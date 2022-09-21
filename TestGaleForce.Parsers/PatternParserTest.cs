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
            var pp = new PatternParser { Content = urlRoot + urlSuffix, Instructions = "^C:?|S:@L:?~*" };
            pp.Parse();
            Assert.AreEqual(urlRoot, pp.Result, "Root of url not parsed");
        }

        [TestMethod]
        public void TestLeftString2()
        {
            var urlRoot = "http://abc.com/one/two";
            var urlSuffix = "";
            var pp = new PatternParser { Content = urlRoot + urlSuffix, Instructions = "^C:?|S:@L:?~*" };
            pp.Parse();
            Assert.AreEqual(urlRoot, pp.Result, "Root of url not parsed");
        }
    }
}
