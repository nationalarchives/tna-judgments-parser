
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

using UK.Gov.NationalArchives.CaseLaw.Parse;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.UnitTests {

public class Test1 {

    private string xml = @"<w:p xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main' w:rsidR='0094552B' w:rsidRPr='0094552B' w:rsidRDefault='0006029F' w:rsidP='009B216B'>
    <w:pPr>
        <w:spacing w:after='240'/>
        <w:ind w:left='567' w:hanging='567'/>
        <w:jc w:val='both'/>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
    </w:pPr>
    <w:r>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:t>2</w:t>
    </w:r>
    <w:r w:rsidR='00A54610'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:t>6</w:t>
    </w:r>
    <w:r>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:tab/>
    </w:r>
    <w:r w:rsidR='0094552B' w:rsidRPr='0094552B'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:t xml:space='preserve'>In any event, there is a conceptual difficulty with Ms Fatima’s argument on limb (a). She conceded in argument that RAVEC could properly express a </w:t>
    </w:r>
    <w:r w:rsidR='0094552B' w:rsidRPr='0094552B'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
            <w:u w:val='single'/>
        </w:rPr>
        <w:t>view</w:t>
    </w:r>
    <w:r w:rsidR='0094552B' w:rsidRPr='0094552B'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:t xml:space='preserve'> on the “in principle” issue. Her complaint was that RAVEC was purporting to </w:t>
    </w:r>
    <w:r w:rsidR='0094552B' w:rsidRPr='0094552B'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
            <w:u w:val='single'/>
        </w:rPr>
        <w:t>decide</w:t>
    </w:r>
    <w:r w:rsidR='0094552B' w:rsidRPr='0094552B'>
        <w:rPr>
            <w:rFonts w:eastAsia='Calibri' w:cs='Times New Roman (Body CS)'/>
        </w:rPr>
        <w:t xml:space='preserve'> that issue. In other words, her real complaint is not that RAVEC had no business considering the issue, but that RAVEC’s view should not be regarded as determinative. If she is right, it would follow that were the chief officer of police to regard RAVEC’s view as determinative, his or her decision would be challengeable for surrendering or fettering his or her discretion. Even if such a claim had merit, there has been no application to the Commissioner (or any other chief police officer) to exercise his discretion under s. 25(1). The only decisions challenged in this claim are those of the Home Secretary and RAVEC. </w:t>
    </w:r>
</w:p>
";

    [Fact]
    public void Test() {
        Paragraph p = new Paragraph(xml);
        WText number = PreParser.GetPlainNumberFromParagraph(p);
        Assert.Equal(@"26", number.Text);
    }

}

}
