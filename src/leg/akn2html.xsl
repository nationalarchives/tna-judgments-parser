<?xml version="1.0" encoding="utf-8"?>

<xsl:transform xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="2.0"
	xpath-default-namespace="http://docs.oasis-open.org/legaldocml/ns/akn/3.0"
	xmlns:uk="https://caselaw.nationalarchives.gov.uk/akn"
	xmlns:html="http://www.w3.org/1999/xhtml"
	xmlns:math="http://www.w3.org/1998/Math/MathML"
	xmlns:xs="http://www.w3.org/2001/XMLSchema"
	exclude-result-prefixes="uk html math xs">

<xsl:output method="html" version="5" encoding="utf-8" indent="yes" include-content-type="no" />

<xsl:strip-space elements="*" />
<xsl:preserve-space elements="p block num heading span a date docDate docNumber docTitle docType docketNumber judge lawyer location neutralCitation party role time b i u" />

<xsl:param name="image-base" as="xs:string" select="'/'" />

<!-- global variables -->

<xsl:variable name="doc-id" as="xs:string">
	<xsl:variable name="work-uri" as="xs:string">
		<xsl:sequence select="/akomaNtoso/*/meta/identification/FRBRWork/FRBRthis/@value" />
	</xsl:variable>
	<xsl:variable name="long-form-prefix" as="xs:string" select="'http://legislation.gov.uk/id/'" />
	<xsl:choose>
		<xsl:when test="starts-with($work-uri, $long-form-prefix)">
			<xsl:sequence select="substring-after($work-uri, $long-form-prefix)" />
		</xsl:when>
		<xsl:otherwise>
			<xsl:sequence select="$work-uri" />
		</xsl:otherwise>
	</xsl:choose>
</xsl:variable>

<!-- templates -->

<xsl:template match="akomaNtoso">
	<html>
		<head>
			<meta charset="utf-8" />
			<xsl:call-template name="style" />
		</head>
		<body>
			<xsl:apply-templates />
		</body>
	</html>
</xsl:template>

<xsl:template name="style">
	<style>
article { margin: 0.5in 1in }
p.center { text-align: center }
section { position: relative }
h2 { font-size: inherit; font-weight: normal }
.section &gt; h2 &gt; .num { display: inline-block; width: 0.5in }
.paragraph { margin-left: 0.5in }
.paragraph &gt; h2 { position: absolute; margin-top: 0; margin-left: -0.5in }
.subparagraph { margin-left: 0.5in }
.subparagraph &gt; h2 { position: absolute; margin-top: 0; margin-left: -0.375in }
section &gt; .level &gt; h2 { margin-left: 0.5in }
table { border-collapse: collapse }
th, td { border: thin dotted; padding: 3pt }
td:has(p.ia-title) { padding: 0 }
td { vertical-align: top }
span.fn { vertical-align: super; font-size: small }
.footnote &gt; p:first-child &gt; .marker:first-child { vertical-align: super; font-size: small }
.blockContainer { position: relative; margin-left: 0.5in }
.blockContainer &gt; p:first-child &gt; .num:first-child { position: absolute; margin-left: -0.25in }
.attachment { margin-top: 2em }

/* Impact Assessment styles */
article[data-doc-type='ImpactAssessment'] {
	font-family: Arial, sans-serif;
}

article[data-doc-type='ImpactAssessment'] * {
	font-family: Arial, sans-serif !important;
}

article[data-doc-type='ImpactAssessment'] .paragraph:not(.num) {
	margin-left: 0 !important;
}

/* IA paragraph classes */
.ia-table-text { font-size: 11pt; margin: 2pt 4pt; line-height: 1.2 }
.ia-head-label { font-size: 12pt; margin: 2pt 4pt }
.ia-title { font-size: 16pt; background: #000; color: #fff; margin: 0; padding: 8pt; text-align: center }
.ia-header-text { font-size: 10pt; margin: 2pt 4pt }
.ia-stage { font-size: 11pt; margin: 2pt 4pt }

/* IA table styling */
.ia-table {
	border: 1px solid black;
	width: 100%;
	margin: 6pt 0;
}

.ia-table th,
.ia-table td {
	border: 1px solid black;
	padding: 4pt 6pt;
	vertical-align: top;
}

.ia-table td p {
	margin: 2pt 4pt;
	line-height: 1.2;
}

.ia-table td p:empty {
	margin: 0;
	height: 4pt;
}

.ia-table td:empty {
	display: none !important;
}

/* Center spanning header cells */
.ia-table tr:first-child td[colspan="5"] .ia-table-text {
	text-align: center;
}

/* Special styling for first level tables */
article[data-doc-type='ImpactAssessment'] .level:first-child .ia-table {
	border: none;
}

article[data-doc-type='ImpactAssessment'] .level:first-child .ia-table td table {
	border: 1px solid black;
	margin: 0;
}

article[data-doc-type='ImpactAssessment'] .level:first-child .ia-table tr:first-child td:nth-child(2) {
	border: none;
	padding: 0;
}

article[data-doc-type='ImpactAssessment'] .level:first-child .ia-table tr:nth-child(2) td:nth-child(2) {
	background: #d9d9d9;
}

/* Special styling for second level tables */
article[data-doc-type='ImpactAssessment'] .level:nth-child(2) .ia-table tr:first-child td,
article[data-doc-type='ImpactAssessment'] .level:nth-child(2) .ia-table tr:nth-child(2) td {
	background: #d9d9d9;
}

/* Hide extra rows in nested tables */
article[data-doc-type='ImpactAssessment'] .level:first-child table td table tr:nth-child(n+7) {
	display: none !important;
}



</style>
<!--	
td { position: relative; min-width: 2em; padding-left: 1em; padding-right: 1em; vertical-align: top }
td > .num { left: -2em }
table { margin: 0 auto; width: 100%; border-collapse: collapse }
.header table { table-layout: fixed }
td > p:first-child { margin-top: 0 }
td > p:last-child { margin-bottom: 0 }
.fn { vertical-align: super; font-size: small }
.footnote > p > .marker { vertical-align: super; font-size: small }
.tab { display: inline-block; width: 0.25in } -->
</xsl:template>

<xsl:template match="doc">
	<article id="doc" data-doc-type="{@name}">
		<xsl:apply-templates />
		<xsl:call-template name="footnotes" />
	</article>
</xsl:template>

<xsl:template match="attachment/doc">
	<section class="attachment" data-name="{ @name }">
		<xsl:apply-templates />
	</section>
</xsl:template>

<xsl:template match="meta" />

<xsl:template match="preface">
	<div class="{ local-name() }">
		<xsl:apply-templates />
	</div>
</xsl:template>

<xsl:template match="mainBody">
	<div class="body">
		<xsl:apply-templates />
	</div>
</xsl:template>

<xsl:template match="level | section | paragraph | subparagraph">
	<section>
		<xsl:attribute name="class">
			<xsl:value-of select="local-name(.)" />
			<xsl:if test="num">
				<xsl:text> num</xsl:text>
			</xsl:if>
			<xsl:if test="heading">
				<xsl:text> heading</xsl:text>
			</xsl:if>
		</xsl:attribute>
		<xsl:if test="num | heading">
			<h2>
				<xsl:apply-templates select="num | heading" />
			</h2>
		</xsl:if>
		<xsl:apply-templates select="* except (num, heading)" />
	</section>
</xsl:template>

<xsl:template match="blockContainer">
	<div class="blockContainer">
		<xsl:apply-templates select="* except num" />
	</div>
</xsl:template>

<xsl:template match="blockContainer/p[1]">
	<p>
		<xsl:apply-templates select="preceding-sibling::num" />
		<xsl:apply-templates />
	</p>
</xsl:template>

<!-- embedded structures -->

<xsl:template match="block[@name='embeddedStructure']">
	<xsl:apply-templates />
</xsl:template>

<xsl:template match="embeddedStructure">
	<blockquote>
		<xsl:apply-templates />
	</blockquote>
</xsl:template>

<!-- blocks -->

<xsl:template match="p">
	<xsl:element name="{ local-name() }">
		<xsl:copy-of select="@class" />
		<xsl:apply-templates />
	</xsl:element>
</xsl:template>

<xsl:template match="block">
	<p class="@name">
		<xsl:apply-templates />
	</p>
</xsl:template>

<!-- inline -->

<xsl:template match="num | heading | docType | docNumber | date">
	<span class="{ local-name() }">
		<xsl:apply-templates />
	</span>
</xsl:template>

<xsl:template match="span">
	<span>
		<xsl:apply-templates />
	</span>
</xsl:template>

<xsl:template match="img">
	<img>
		<xsl:attribute name="src">
			<xsl:sequence select="concat($image-base, $doc-id, '/', @src)" />
		</xsl:attribute>
		<xsl:apply-templates />
	</img>
</xsl:template>

<xsl:template match="b | i | u">
	<xsl:element name="{ local-name() }">
		<xsl:apply-templates />
	</xsl:element>
</xsl:template>

<xsl:template match="br">
	<br/>
</xsl:template>


<!-- tables -->

<xsl:template match="table">
	<table>
		<xsl:copy-of select="@class | @style" />
		<!-- Add Impact Assessment table class based on context -->
		<xsl:if test="ancestor::doc[@name='ImpactAssessment']">
			<xsl:attribute name="class">
				<xsl:text>ia-table</xsl:text>
				<xsl:if test="@class">
					<xsl:text> </xsl:text>
					<xsl:value-of select="@class" />
				</xsl:if>
			</xsl:attribute>
		</xsl:if>
		<xsl:if test="exists(@uk:widths)">
			<colgroup>
				<xsl:for-each select="tokenize(@uk:widths, ' ')">
					<col style="width:{.}" />
				</xsl:for-each>
			</colgroup>
		</xsl:if>
		<tbody>
			<xsl:apply-templates />
		</tbody>
	</table>
</xsl:template>

<xsl:template match="tr | td">
	<xsl:element name="{ local-name() }">
		<xsl:copy-of select="@*" />
		<xsl:apply-templates />
	</xsl:element>
</xsl:template>


<!-- links -->

<xsl:template match="a | ref">
	<a>
		<xsl:apply-templates select="@href" />
		<xsl:apply-templates />
	</a>
</xsl:template>


<!-- tables of contents -->

<xsl:template match="toc">
	<div class="toc">
		<xsl:apply-templates />
	</div>
</xsl:template>

<xsl:template match="tocItem">
	<p class="tocItem">
		<xsl:apply-templates />
	</p>
</xsl:template>


<!-- markers and attributes -->

<xsl:template match="marker[@name='tab']">
	<span class="tab">&#160;</span>
</xsl:template>

<xsl:template match="@src | @href | @title">
	<xsl:copy />
</xsl:template>


<!-- footnotes -->

<xsl:template match="authorialNote">
	<span class="fn">
		<xsl:value-of select="@marker" />
	</span>
</xsl:template>

<xsl:template name="footnotes">
	<xsl:param name="footnotes" as="element()*" select="descendant::authorialNote" />
	<xsl:if test="exists($footnotes)">
		<footer class="footnotes">
			<hr style="margin-top:2em" />
			<xsl:apply-templates select="$footnotes" mode="footnote" />
		</footer>
	</xsl:if>
</xsl:template>

<xsl:template match="authorialNote" mode="footnote">
	<div class="footnote">
		<xsl:apply-templates />
	</div>
</xsl:template>

<xsl:template match="authorialNote/p[1]">
	<xsl:element name="{ local-name() }">
		<xsl:if test="@class">
			<xsl:attribute name="class" select="@class" />
		</xsl:if>
		<span class="marker">
			<xsl:value-of select="../@marker" />
		</span>
		<xsl:text> </xsl:text>
		<xsl:apply-templates />
	</xsl:element>
</xsl:template>


<!-- math -->

<xsl:template match="math:*">
	<xsl:copy>
		<xsl:copy-of select="@*"/>
		<xsl:apply-templates />
	</xsl:copy>
</xsl:template>

</xsl:transform>
