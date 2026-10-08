
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw;

class JudgmentBuilder : Builder {

    override protected string UKNS => Metadata.ukns;

    /// <summary>
    /// Looks for a previously generated id for the given division and returns null if not found
    /// </summary>
    /// <param name="division"></param>
    /// <returns>division id or null</returns>
    override protected string MakeDivisionId(IDivision division)
    {
       return DivHashToId.GetValueOrDefault(division);
    }

    public static XmlDocument Build(IJudgment judgment) {
        JudgmentBuilder builder = new(judgment);
        builder.Build1(judgment);
        AddHash(builder.doc, builder.UKNS);
        return builder.doc;
    }

    private readonly IJudgment Judgment;

    private JudgmentBuilder(IJudgment judgment) {
        Judgment = judgment;
        IEnumerable<IDivision> divisions = judgment.Body.SelectMany(dec => dec.Contents);
        GenerateIds(divisions);
    }

    /* generate ids */

    private readonly HashSet<string> UsedParagraphIds = new();

    private string MakeParagraphnId(IDivision division) {
        if (division.Name != "paragraph")
            return null;
        if (division.Number is null)
            return null;
        Match match = Regex.Match(division.Number.Text, @"^(\d+)\.?$");
        if (!match.Success)
            return null;
        string id = "para_" + match.Groups[1].Value;
        bool isNew = this.UsedParagraphIds.Add(id);
        if (!isNew)
            return null;
        return id;
    }

    readonly Dictionary<IDivision, string> DivHashToId = new();

    private void GenerateIds(IEnumerable<IDivision> divisions, string parent = null) {
        int lvl = 0;
        foreach (var div in divisions) {
            string para = MakeParagraphnId(div);
            if (para is not null) {
                DivHashToId.Add(div, para);
                continue;
            }
            string prefix = parent ?? "lvl";
            lvl += 1;
            string id = prefix + "_"+ lvl;
            DivHashToId.Add(div, id);
            if (div.Name == "paragraph")
                continue;
            if (div is not IBranch branch)
                continue;
            GenerateIds(branch.Children, id);
        }
    }

    private string GetIdForInternalLinkTarget(string target) {
        foreach (var dec in Judgment.Body) {
            foreach (var div in dec.Contents) {
                string id = GetIdForInternalLinkTarget(div, target);
                if (id is not null)
                    return id;
            }
        }
        return null;
    }

    private string GetIdForInternalLinkTarget(IDivision div, string target) {
        if (div is ILeaf leaf) {
            if (GetBookmarks(leaf).Any(bkmrk => bkmrk.Name == target))
                return MakeDivisionId(div);
        } else if (div is IBranch branch) {
            if (div.Name == "paragraph") {
                if (GetBookmarks(branch).Any(bkmrk => bkmrk.Name == target))
                    return MakeDivisionId(div);
            } else {
                if (GetBookmarks(branch.Heading).Any(bkmrk => bkmrk.Name == target))
                    return MakeDivisionId(div);
                if (GetBookmarks(branch.Intro).Any(bkmrk => bkmrk.Name == target))
                    return MakeDivisionId(div);
                foreach (var sub in branch.Children) {
                    string id = GetIdForInternalLinkTarget(sub, target);
                    if (id is not null)
                        return id;
                }
                if (GetBookmarks(branch.WrapUp).Any(bkmrk => bkmrk.Name == target))
                    return MakeDivisionId(div);
            }
        }
        return null;
    }

    private static IEnumerable<WBookmark> GetBookmarks(IDivision div) {
        if (div is ILeaf leaf)
            return GetBookmarks(leaf);
        if (div is IBranch branch)
            return GetBookmarks(branch);
        throw new System.Exception();
    }
    private static IEnumerable<WBookmark> GetBookmarks(ILeaf leaf) {
        var heading = GetBookmarks(leaf.Heading);
        var conents = GetBookmarks(leaf.Contents);
        return heading.Concat(conents);
    }
    private static IEnumerable<WBookmark> GetBookmarks(IBranch branch) {
        var heading = (branch.Heading is WLine line) ? line.Bookmarks : Enumerable.Empty<WBookmark>();
        var intro = GetBookmarks(branch.Intro);
        var children = branch.Children.SelectMany(GetBookmarks);
        var wrapUp = GetBookmarks(branch.WrapUp);
        return heading.Concat(intro).Concat(children).Concat(wrapUp);
    }
    private static IEnumerable<WBookmark> GetBookmarks(IEnumerable<IBlock> contents) {
        if (contents is null)
            return Enumerable.Empty<WBookmark>();
        return contents.SelectMany(Util.GetLines)
            .Where(line => line is WLine).Cast<WLine>()
            .SelectMany(line => line.Bookmarks);
    }
    private static IEnumerable<WBookmark> GetBookmarks(ILine heading) {
        // if (heading is null)
        //     return Enumerable.Empty<WBookmark>();
        if (heading is WLine line)
            return line.Bookmarks;
        return Enumerable.Empty<WBookmark>();
    }

    private readonly ILogger Logger = Logging.Factory.CreateLogger<JudgmentBuilder>();

    protected override void AddInternalLink(XmlElement parent, IInternalLink link) {
        var id = GetIdForInternalLinkTarget(link.Target);
        if (id is null) {
            Logger.LogWarning("can't find id for link target {}", link.Target);
            base.AddInternalLink(parent, link);
            return;
        }
        XmlElement a = CreateAndAppend("a", parent);
        a.SetAttribute("href", "#" + id);
        AddInlineContainerContents(a, link.Contents);
    }

}
