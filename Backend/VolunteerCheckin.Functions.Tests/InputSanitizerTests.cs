using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class InputSanitizerTests
{
    #region SanitizeString - Null and Empty

    [TestMethod]
    public void SanitizeString_Null_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeString(null);

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizeString_Empty_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeString("");

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizeString_Whitespace_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeString("   ");

        result.ShouldBe(string.Empty);
    }

    #endregion

    #region SanitizeString - Trimming

    [TestMethod]
    public void SanitizeString_LeadingWhitespace_Trims()
    {
        string result = InputSanitizer.SanitizeString("  hello");

        result.ShouldBe("hello");
    }

    [TestMethod]
    public void SanitizeString_TrailingWhitespace_Trims()
    {
        string result = InputSanitizer.SanitizeString("hello  ");

        result.ShouldBe("hello");
    }

    [TestMethod]
    public void SanitizeString_BothEndsWhitespace_Trims()
    {
        string result = InputSanitizer.SanitizeString("  hello world  ");

        result.ShouldBe("hello world");
    }

    #endregion

    #region SanitizeString - HTML Removal

    [TestMethod]
    public void SanitizeString_SimpleHtmlTag_RemovesTag()
    {
        string result = InputSanitizer.SanitizeString("<b>bold text</b>");

        result.ShouldBe("bold text");
    }

    [TestMethod]
    public void SanitizeString_MultipleHtmlTags_RemovesAll()
    {
        string result = InputSanitizer.SanitizeString("<div><p>Hello</p><span>World</span></div>");

        result.ShouldBe("HelloWorld");
    }

    [TestMethod]
    public void SanitizeString_HtmlWithAttributes_RemovesTagsWithAttributes()
    {
        string result = InputSanitizer.SanitizeString("<a href=\"http://evil.com\">Click me</a>");

        result.ShouldBe("Click me");
    }

    [TestMethod]
    public void SanitizeString_SelfClosingTags_Removes()
    {
        string result = InputSanitizer.SanitizeString("Hello<br/>World<img src=\"x\"/>");

        result.ShouldBe("HelloWorld");
    }

    [TestMethod]
    public void SanitizeString_MalformedHtml_StillRemoves()
    {
        string result = InputSanitizer.SanitizeString("<div>unclosed");

        result.ShouldBe("unclosed");
    }

    #endregion

    #region SanitizeString - Script Removal

    [TestMethod]
    public void SanitizeString_ScriptTag_RemovesTags()
    {
        // Note: Current implementation removes HTML tags but leaves script content
        // The content between script tags remains after tag removal
        string result = InputSanitizer.SanitizeString("<script>alert('xss')</script>Safe content");

        // Tags are removed but content remains
        result.ShouldBe("alert('xss')Safe content");
    }

    [TestMethod]
    public void SanitizeString_ScriptTagUppercase_RemovesTags()
    {
        string result = InputSanitizer.SanitizeString("<SCRIPT>alert('xss')</SCRIPT>Safe");

        result.ShouldBe("alert('xss')Safe");
    }

    [TestMethod]
    public void SanitizeString_ScriptWithAttributes_RemovesTags()
    {
        string result = InputSanitizer.SanitizeString("<script type=\"text/javascript\">evil()</script>Good");

        result.ShouldBe("evil()Good");
    }

    [TestMethod]
    public void SanitizeString_EmptyScriptTag_RemovesTag()
    {
        // Empty script tags are completely removed
        string result = InputSanitizer.SanitizeString("<script></script>Safe text");

        result.ShouldBe("Safe text");
    }

    #endregion

    #region SanitizeString - Length Truncation

    [TestMethod]
    public void SanitizeString_ExceedsMaxLength_Truncates()
    {
        string longString = new('a', 100);

        string result = InputSanitizer.SanitizeString(longString, 50);

        result.Length.ShouldBe(50);
        result.ShouldBe(new string('a', 50));
    }

    [TestMethod]
    public void SanitizeString_ExactlyMaxLength_NoTruncation()
    {
        string exactString = new('b', 50);

        string result = InputSanitizer.SanitizeString(exactString, 50);

        result.Length.ShouldBe(50);
    }

    [TestMethod]
    public void SanitizeString_UnderMaxLength_NoTruncation()
    {
        string shortString = "hello";

        string result = InputSanitizer.SanitizeString(shortString, 100);

        result.ShouldBe("hello");
    }

    [TestMethod]
    public void SanitizeString_DefaultMaxLength_Is1000()
    {
        string longString = new('x', 1500);

        string result = InputSanitizer.SanitizeString(longString);

        result.Length.ShouldBe(1000);
    }

    #endregion

    #region SanitizeName

    [TestMethod]
    public void SanitizeName_ValidName_ReturnsSanitized()
    {
        string result = InputSanitizer.SanitizeName("  John Smith  ");

        result.ShouldBe("John Smith");
    }

    [TestMethod]
    public void SanitizeName_LongName_TruncatesTo200()
    {
        string longName = new('A', 250);

        string result = InputSanitizer.SanitizeName(longName);

        result.Length.ShouldBe(200);
    }

    [TestMethod]
    public void SanitizeName_NameWithHtml_RemovesHtml()
    {
        string result = InputSanitizer.SanitizeName("<b>John</b> Smith");

        result.ShouldBe("John Smith");
    }

    [TestMethod]
    public void SanitizeName_Null_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeName(null);

        result.ShouldBe(string.Empty);
    }

    #endregion

    #region SanitizeDescription

    [TestMethod]
    public void SanitizeDescription_ValidDescription_ReturnsSanitized()
    {
        string result = InputSanitizer.SanitizeDescription("  This is a description.  ");

        result.ShouldBe("This is a description.");
    }

    [TestMethod]
    public void SanitizeDescription_LongDescription_TruncatesTo2000()
    {
        string longDescription = new('D', 2500);

        string result = InputSanitizer.SanitizeDescription(longDescription);

        result.Length.ShouldBe(2000);
    }

    [TestMethod]
    public void SanitizeDescription_Null_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeDescription(null);

        result.ShouldBe(string.Empty);
    }

    #endregion

    #region SanitizeNotes

    [TestMethod]
    public void SanitizeNotes_ValidNotes_ReturnsSanitized()
    {
        string result = InputSanitizer.SanitizeNotes("  Some notes here.  ");

        result.ShouldBe("Some notes here.");
    }

    [TestMethod]
    public void SanitizeNotes_LongNotes_TruncatesTo5000()
    {
        string longNotes = new('N', 6000);

        string result = InputSanitizer.SanitizeNotes(longNotes);

        result.Length.ShouldBe(5000);
    }

    [TestMethod]
    public void SanitizeNotes_Null_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizeNotes(null);

        result.ShouldBe(string.Empty);
    }

    #endregion

    #region SanitizeEmail

    [TestMethod]
    public void SanitizeEmail_ValidEmail_ReturnsLowercased()
    {
        string? result = InputSanitizer.SanitizeEmail("John.Smith@Example.COM");

        result.ShouldBe("john.smith@example.com");
    }

    [TestMethod]
    public void SanitizeEmail_ValidEmailWithWhitespace_Trims()
    {
        string? result = InputSanitizer.SanitizeEmail("  user@example.com  ");

        result.ShouldBe("user@example.com");
    }

    [TestMethod]
    public void SanitizeEmail_Null_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail(null);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_Empty_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail("");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_Whitespace_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail("   ");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_InvalidFormat_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail("not-an-email");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_MissingAtSymbol_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail("userexample.com");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_MissingDomain_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeEmail("user@");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_TooLong_ReturnsNull()
    {
        // RFC 5321 max is 254 characters
        string longLocalPart = new('a', 250);
        string longEmail = $"{longLocalPart}@example.com";

        string? result = InputSanitizer.SanitizeEmail(longEmail);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeEmail_ExactlyMaxLength_ReturnsEmail()
    {
        // 254 characters total
        string localPart = new('a', 241); // 241 + @ + example.com (11) = 253
        string email = $"{localPart}@example.com";

        string? result = InputSanitizer.SanitizeEmail(email);

        // Should succeed if valid format
        result.ShouldNotBeNull();
    }

    #endregion

    #region SanitizePhone

    [TestMethod]
    public void SanitizePhone_ValidPhone_KeepsDigitsAndAllowedChars()
    {
        string? result = InputSanitizer.SanitizePhone("+1 (555) 123-4567");

        result.ShouldBe("+1 (555) 123-4567");
    }

    [TestMethod]
    public void SanitizePhone_PhoneWithLetters_RemovesLetters()
    {
        // "555-CALL-NOW" has 2 dashes, letters are removed
        string? result = InputSanitizer.SanitizePhone("555-CALL-NOW");

        result.ShouldBe("555--");
    }

    [TestMethod]
    public void SanitizePhone_PhoneWithSpecialChars_RemovesInvalidChars()
    {
        string? result = InputSanitizer.SanitizePhone("555#123$456");

        result.ShouldBe("555123456");
    }

    [TestMethod]
    public void SanitizePhone_Null_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizePhone(null);

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizePhone_Empty_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizePhone("");

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizePhone_Whitespace_ReturnsEmpty()
    {
        string result = InputSanitizer.SanitizePhone("   ");

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizePhone_TooLong_TruncatesTo30()
    {
        string longPhone = new('1', 50);

        string result = InputSanitizer.SanitizePhone(longPhone);

        result.Length.ShouldBe(30);
    }

    [TestMethod]
    public void SanitizePhone_LeadingWhitespace_Trims()
    {
        string result = InputSanitizer.SanitizePhone("  555-1234");

        result.ShouldBe("555-1234");
    }

    #endregion

    #region SanitizeWhat3Words

    [TestMethod]
    public void SanitizeWhat3Words_ValidFormat_ReturnsLowercased()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("Filled.Count.Soap");

        result.ShouldBe("filled.count.soap");
    }

    [TestMethod]
    public void SanitizeWhat3Words_WithWhitespace_Trims()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("  word.word.word  ");

        result.ShouldBe("word.word.word");
    }

    [TestMethod]
    public void SanitizeWhat3Words_Null_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words(null);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_Empty_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_Whitespace_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("   ");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_InvalidFormat_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("not.valid");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_TooLong_ReturnsNull()
    {
        string longW3W = new string('a', 20) + "." + new string('b', 20) + "." + new string('c', 20);

        string? result = InputSanitizer.SanitizeWhat3Words(longW3W);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_TwoWords_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("only.two");

        result.ShouldBeNull();
    }

    [TestMethod]
    public void SanitizeWhat3Words_FourWords_ReturnsNull()
    {
        string? result = InputSanitizer.SanitizeWhat3Words("too.many.words.here");

        result.ShouldBeNull();
    }

    #endregion

    #region XSS Prevention

    [TestMethod]
    public void SanitizeString_XssOnClickHandler_RemovesHandler()
    {
        string result = InputSanitizer.SanitizeString("<div onclick=\"alert('xss')\">Click</div>");

        result.ShouldBe("Click");
    }

    [TestMethod]
    public void SanitizeString_XssImgOnerror_RemovesTag()
    {
        string result = InputSanitizer.SanitizeString("<img src=x onerror=\"alert('xss')\">");

        result.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void SanitizeString_XssIframe_RemovesTag()
    {
        string result = InputSanitizer.SanitizeString("<iframe src=\"http://evil.com\"></iframe>Safe");

        result.ShouldBe("Safe");
    }

    [TestMethod]
    public void SanitizeString_XssStyleExpression_RemovesTag()
    {
        string result = InputSanitizer.SanitizeString("<div style=\"background:url(javascript:alert('xss'))\">Text</div>");

        result.ShouldBe("Text");
    }

    #endregion
}
