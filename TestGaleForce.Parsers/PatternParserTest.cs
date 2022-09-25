//-----------------------------------------------------------------------
// <copyright file="PatternParserTest.cs" company="Jim Gale">
// Copyright (C) Jim Gale. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace TestGaleForceCore.Parsers
{
    using GaleForceCore.Parsers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Defines test class PatternParserTest.
    /// </summary>
    [TestClass]
    public class PatternParserTest
    {
        /// <summary>
        /// Defines the test method TestLeftString.
        /// </summary>
        [TestMethod]
        public void TestLeftString()
        {
            var urlRoot = "http://abc.com/one/two";
            var urlSuffix = "?key=value";
            var pp = new PatternParser { Content = urlRoot + urlSuffix, Instructions = "^C:?|S:@L:?~*", IsDebug = true };
            pp.Parse();
            Assert.AreEqual(urlRoot, pp.Result, "Root of url not parsed");
        }

        [TestMethod]
        public void TestLeftString2()
        {
            var urlRoot = "http://abc.com/one/two";
            var urlSuffix = string.Empty;
            var pp = new PatternParser { Content = urlRoot + urlSuffix, Instructions = "^C:?|S:@L:?~*", IsDebug = true };
            pp.Parse();
            Assert.AreEqual(urlRoot, pp.Result, "Root of url not parsed");
        }

        [TestMethod]
        public void TestDivLoc1()
        {
            var pp = new PatternParser
            {
                Content = GetHtml1(),
                Instructions = "AQ:.lead|T:|C*:*A full message*|S>:~*|C><:*|S>:notfound~found",
                IsDebug = true
            };
            pp.Parse();
            Assert.AreEqual("found", pp.Result, "parse failed");
        }

        private string GetHtml1()
        {
            return
                "<html><head></head><body>" +
                "<div>" +
                "<div class='lead extra'>" +
                "<span>Something like A full message.</span>" +
                "</div>" +
                "</div>" +
                "</body></html>";
        }
    }
}
