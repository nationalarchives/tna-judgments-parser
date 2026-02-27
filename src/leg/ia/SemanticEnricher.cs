using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Wordprocessing;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.ImpactAssessments {

class SemanticEnricher : Enricher {

    private static readonly Dictionary<string, (Type elementType, string nameAttribute)> SemanticMappings = new() {
        { "Title:", (typeof(Models.DocTitle), null) },
        { "Stage:", (typeof(Models.DocStage), null) },
        { "Date:", (typeof(Models.DocDate), null) },
        { "Lead department or agency:", (typeof(Models.LeadDepartment), "leadDepartment") },
        { "Other departments or agencies", (typeof(Models.OtherDepartments), "otherDepartments") }
    };

    internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks) {
        var blockList = blocks is List<IBlock> list ? list : new List<IBlock>(blocks);
        if (blockList.Count == 0)
            return blocks;

        var enriched = new List<IBlock>();
        int i = 0;

        while (i < blockList.Count) {
            IBlock current = blockList[i];

            if (current is WLine line) {
                string text = GetLineText(line);
                SemanticPattern pattern = DetectSemanticPattern(text);

                if (pattern != null) {
                    var (continuationBlocks, nextIndex) = CollectContinuationBlocks(blockList, i, pattern);
                    IBlock enrichedBlock = CreateSemanticBlock(line, continuationBlocks, pattern);
                    enriched.Add(enrichedBlock);
                    
                    i = nextIndex;
                    continue;
                }
            } else if (current is WTable table) {
                WTable enrichedTable = EnrichTable(table);
                enriched.Add(enrichedTable);
                i++;
                continue;
            }

            enriched.Add(current);
            i++;
        }

        return enriched;
    }

    private WTable EnrichTable(WTable table) {
        var enrichedRows = new List<WRow>();
        bool changed = false;

        foreach (var row in table.TypedRows) {
            WRow enrichedRow = EnrichRow(row);
            enrichedRows.Add(enrichedRow);
            if (!object.ReferenceEquals(row, enrichedRow))
                changed = true;
        }

        return changed ? new WTable(table.Main, table.Properties, table.Grid, enrichedRows) : table;
    }

    private WRow EnrichRow(WRow row) {
        var enrichedCells = new List<WCell>();
        bool changed = false;

        foreach (var cell in row.TypedCells) {
            WCell enrichedCell = EnrichCell(cell);
            enrichedCells.Add(enrichedCell);
            if (!object.ReferenceEquals(cell, enrichedCell))
                changed = true;
        }

        return changed ? new WRow(row.Table, row.TablePropertyExceptions, row.Properties, enrichedCells) : row;
    }

    private WCell EnrichCell(WCell cell) {
        var enrichedBlocks = new List<IBlock>();
        bool changed = false;

        foreach (var block in cell.Contents) {
            IBlock enriched = EnrichBlock(block);
            enrichedBlocks.Add(enriched);
            if (!object.ReferenceEquals(block, enriched))
                changed = true;
        }

        return changed ? new WCell(cell.Row, cell.Props, enrichedBlocks) : cell;
    }

    private IBlock EnrichBlock(IBlock block) {
        if (block is WLine line) {
            string text = GetLineText(line);
            SemanticPattern pattern = DetectSemanticPattern(text);
            if (pattern != null) {
                return CreateSemanticBlock(line, new List<IBlock>(), pattern);
            }
        } else if (block is WTable table) {
            return EnrichTable(table);
        }
        return block;
    }

    private string GetLineText(WLine line) {
        return string.Join("", line.Contents
            .OfType<Judgments.IFormattedText>()
            .Select(ft => ft.Text))
            .Trim();
    }

    private SemanticPattern DetectSemanticPattern(string text) {
        foreach (var mapping in SemanticMappings) {
            if (text.StartsWith(mapping.Key, StringComparison.InvariantCultureIgnoreCase)) {
                return new SemanticPattern {
                    Label = mapping.Key,
                    ElementType = mapping.Value.elementType,
                    NameAttribute = mapping.Value.nameAttribute,
                    ValueStartIndex = mapping.Key.Length
                };
            }
        }
        return null;
    }

    private (List<IBlock> continuationBlocks, int nextIndex) CollectContinuationBlocks(
        List<IBlock> blocks, int startIndex, SemanticPattern pattern) {
        
        var continuation = new List<IBlock>();
        int i = startIndex + 1;

        while (i < blocks.Count) {
            IBlock next = blocks[i];

            if (next is not WLine nextLine)
                break;

            string nextText = GetLineText(nextLine);
            if (DetectSemanticPattern(nextText) != null)
                break;

            if (IsContinuationBlock(nextLine, nextText, pattern)) {
                continuation.Add(next);
                i++;
            } else {
                break;
            }
        }

        return (continuation, i);
    }

    private bool IsContinuationBlock(WLine line, string text, SemanticPattern pattern) {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.Length > 100)
            return false;

        if (text.Contains(":"))
            return false;

        if (pattern.NameAttribute != null) {
            return true;
        }

        return false;
    }

    private IBlock CreateSemanticBlock(WLine labelLine, List<IBlock> continuationBlocks, SemanticPattern pattern) {
        var (labelInlines, valueInlines) = SplitInlinesAtLabel(labelLine, pattern.Label);
        var allValueInlines = new List<Judgments.IInline>(valueInlines);
        
        foreach (var block in continuationBlocks) {
            if (block is WLine contLine) {
                if (allValueInlines.Count > 0 && !string.IsNullOrWhiteSpace(GetLineText(contLine))) {
                    allValueInlines.Add(new Judgments.Parse.WText(" ", null));
                }
                allValueInlines.AddRange(contLine.Contents);
            }
        }

        var newInlines = new List<Judgments.IInline>(labelInlines);
        
        if (allValueInlines.Count > 0) {
            if (newInlines.Count > 0) {
                newInlines.Add(new Judgments.Parse.WText(" ", null));
            }
            Models.InlineContainer semanticElement = CreateSemanticElement(pattern.ElementType, pattern.NameAttribute, allValueInlines);
            newInlines.Add(semanticElement);
        }

        return WLine.Make(labelLine, newInlines);
    }

    private (List<Judgments.IInline> labelInlines, List<Judgments.IInline> valueInlines) SplitInlinesAtLabel(
        WLine line, string labelPattern) {
        
        var labelInlines = new List<Judgments.IInline>();
        var valueInlines = new List<Judgments.IInline>();
        
        string accumulatedText = "";
        bool foundLabel = false;
        int labelEndIndex = -1;
        
        foreach (var inline in line.Contents) {
            if (foundLabel) {
                valueInlines.Add(inline);
            } else {
                if (inline is Judgments.IFormattedText ft) {
                    string text = ft.Text;
                    accumulatedText += text;
                    labelInlines.Add(inline);
                    
                    if (accumulatedText.StartsWith(labelPattern, StringComparison.InvariantCultureIgnoreCase)) {
                        foundLabel = true;
                        labelEndIndex = labelPattern.Length;
                        
                        int textStartInAccumulated = accumulatedText.Length - text.Length;
                        if (labelEndIndex > textStartInAccumulated) {
                            int remainingStart = labelEndIndex - textStartInAccumulated;
                            string remaining = text.Substring(remainingStart).Trim();
                            if (remaining.StartsWith(":"))
                                remaining = remaining.Substring(1).Trim();
                            
                            if (!string.IsNullOrEmpty(remaining)) {
                                labelInlines.RemoveAt(labelInlines.Count - 1);
                                string labelPart = text.Substring(0, remainingStart);
                                RunProperties props = (ft as Judgments.Parse.WText)?.properties;
                                labelInlines.Add(new Judgments.Parse.WText(labelPart, props));
                                valueInlines.Add(new Judgments.Parse.WText(remaining, props));
                            }
                        }
                    }
                } else {
                    labelInlines.Add(inline);
                    if (!foundLabel) {
                        accumulatedText += " ";
                    }
                }
            }
        }
        
        return (labelInlines, valueInlines);
    }

    private Models.InlineContainer CreateSemanticElement(Type elementType, string nameAttribute, List<Judgments.IInline> contents) {
        if (elementType == typeof(Models.DocTitle)) {
            return new Models.DocTitle { Contents = contents };
        } else if (elementType == typeof(Models.DocStage)) {
            return new Models.DocStage { Contents = contents };
        } else if (elementType == typeof(Models.DocDate)) {
            return new Models.DocDate { Contents = contents };
        } else if (elementType == typeof(Models.LeadDepartment)) {
            return new Models.LeadDepartment { Contents = contents };
        } else if (elementType == typeof(Models.OtherDepartments)) {
            return new Models.OtherDepartments { Contents = contents };
        }
        throw new ArgumentException($"Unknown semantic element type: {elementType}");
    }

    protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line) {
        return line;
    }

    internal new IEnumerable<IDivision> Enrich(IEnumerable<IDivision> divs) {
        List<IDivision> enriched = new List<IDivision>(divs.Count());
        bool changed = false;
        foreach (IDivision div in divs) {
            IDivision enriched1 = Enrich(div);
            enriched.Add(enriched1);
            changed = changed || !object.ReferenceEquals(enriched1, div);
        }
        return changed ? enriched : divs;
    }

    internal override IDivision Enrich(IDivision div) {
        if (div is Models.Section section) {
            var enrichedChildren = Enrich(section.Children).ToList();
            if (object.ReferenceEquals(enrichedChildren, section.Children))
                return section;
            return new Models.Section {
                Number = section.Number,
                Heading = section.Heading,
                Children = enrichedChildren
            };
        }
        
        if (div is Models.Subheading subheading) {
            var enrichedChildren = Enrich(subheading.Children).ToList();
            if (object.ReferenceEquals(enrichedChildren, subheading.Children))
                return subheading;
            return new Models.Subheading {
                Heading = subheading.Heading,
                Children = enrichedChildren
            };
        }
        
        if (div is Models.Subparagraph subpara) {
            var enrichedContents = Enrich(subpara.Contents).ToList();
            if (object.ReferenceEquals(enrichedContents, subpara.Contents))
                return subpara;
            return new Models.Subparagraph {
                Number = subpara.Number,
                Heading = subpara.Heading,
                Contents = enrichedContents
            };
        }
        
        if (div is Models.BranchParagraph bp) {
            var enrichedChildren = Enrich(bp.Children).ToList();
            if (object.ReferenceEquals(enrichedChildren, bp.Children))
                return bp;
            return new Models.BranchParagraph {
                Number = bp.Number,
                Heading = bp.Heading,
                Children = enrichedChildren
            };
        }
        
        return base.Enrich(div);
    }

    private class SemanticPattern {
        public string Label { get; set; }
        public Type ElementType { get; set; }
        public string NameAttribute { get; set; }
        public int ValueStartIndex { get; set; }
    }
}

}
