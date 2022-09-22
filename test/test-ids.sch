<?xml version="1.0" encoding="UTF-8"?>
<sch:schema xmlns="http://purl.oclc.org/dsdl/schematron" xmlns:sch="http://purl.oclc.org/dsdl/schematron" queryBinding="xslt2"
    xmlns:sqf="http://www.schematron-quickfix.com/validator/process"
    xmlns:akn="http://docs.oasis-open.org/legaldocml/ns/akn/3.0">

<ns prefix="akn" uri="http://docs.oasis-open.org/legaldocml/ns/akn/3.0" />

<pattern id="jim">

<rule context="akn:paragraph">

<assert test="exists(@eId)" role="warn">The eId is empty</assert>

<let name="last" value="preceding::akn:paragraph[exists(@eId)][1]" />
<let name="last-num" value="if (empty($last)) then 0 else xs:integer(substring-after($last/@eId, 'para_'))" />
<let name="this-num" value="if (empty(@eId)) then () else xs:integer(substring-after(@eId, 'para_'))" />
<report test="exists($this-num) and $this-num - $last-num = 1" role="info">The eId is correct: <value-of select="@eId"/></report>
<assert test="empty($this-num) or $this-num - $last-num = 1" role="error">The eId is incorrect: <value-of select="@eId"/></assert>

</rule>

</pattern>

</sch:schema>
