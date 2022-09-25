//-----------------------------------------------------------------------
// <copyright file="PatternParser.cs" company="Jim Gale">
// Copyright (C) Jim Gale. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace GaleForceCore.Parsers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Xml;
    using AngleSharp.Html.Parser;
    using GaleForceCore.Helpers;
    using HtmlAgilityPack;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Class PatternParser.
    /// </summary>
    public class PatternParser
    {
        /// <summary>
        /// Delegate CustomParserCaller
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>System.String.</returns>
        public delegate string CustomParserCaller(object sender, CustomParserArguments args);

        /// <summary>
        /// The document
        /// </summary>
        private XmlDocument doc;

        /// <summary>
        /// The keypart
        /// </summary>
        private string keypart;

        /// <summary>
        /// The valuepart
        /// </summary>
        private string valuepart;

        /// <summary>
        /// The nodes
        /// </summary>
        private XmlNodeList nodes;

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the instructions.
        /// </summary>
        public string Instructions { get; set; }

        /// <summary>
        /// Gets or sets the result array.
        /// </summary>
        public JArray ResultArray { get; set; }

        /// <summary>
        /// Gets the dictionary.
        /// </summary>
        public Dictionary<string, string> Dictionary { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dictionary.
        /// </summary>
        public bool IsDictionary { get; set; }

        /// <summary>
        /// Gets or sets the tablename.
        /// </summary>
        public string Tablename { get; set; }

        /// <summary>
        /// Gets or sets the result.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [logical result].
        /// </summary>
        public bool? LogicalResult { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is debug.
        /// </summary>
        public bool IsDebug { get; set; }

        /// <summary>
        /// Gets the debug string.
        /// </summary>
        public StringBuilder DebugString { get; internal set; }

        public List<DebugStep> DebugSteps { get; internal set; } = new List<DebugStep>();

        public int Step { get; internal set; } = 0;

        /// <summary>
        /// Occurs when [custom parser].
        /// </summary>
        public event CustomParserCaller CustomParser;

        /// <summary>
        /// Quicks the parse.
        /// </summary>
        /// <param name="instructions">The instructions.</param>
        /// <returns>System.String.</returns>
        public string QuickParse(string instructions)
        {
            Instructions = instructions;
            var result = Parse();
            return result ? Result : "ERROR:" + DebugString.ToString();
        }

        /// <summary>
        /// Parses this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        /// <exception cref="System.Exception">doc (X:) is null</exception>
        public bool Parse()
        {
            var temp = Content;

            var results = new List<string>();
            var potentialResults = new Stack<string>();
            var prefix = string.Empty;
            DebugString = new StringBuilder();
            XmlDocument doc = null;

            if (IsDebug)
            {
                DebugString.AppendLine();
                DebugString.AppendLine("Instructions:" + Instructions);
            }

            var remainingParses = Instructions;
            var breakTop = false;
            var afterIdentified = false;

            var parses = Instructions.Split('|');
            foreach (var parsex in parses)
            {
                var parse = parsex;

                var isRepeat = true;
                while (isRepeat)
                {
                    isRepeat = false;

                    var step = new DebugStep { BeforeContent = temp, Step = ++Step, Instruction = parse };
                    if (IsDebug)
                    {
                        DebugSteps.Add(step);
                        afterIdentified = false;
                    }

                    if (remainingParses.Length > 0)
                    {
                        remainingParses = remainingParses.Length < parse.Length + 1
                            ? string.Empty
                            : remainingParses.Substring(parse.Length + 1);
                    }

                    var colon = parse.IndexOf(':');
                    if (colon > -1)
                    {
                        if (IsDebug)
                        {
                            DebugString.AppendLine();
                            DebugString.AppendLine("Parse:" + parse);
                            DebugString
                                .AppendLine(
                                    "Before:" +
                                        (string.IsNullOrWhiteSpace(temp)
                                            ? string.Empty
                                            : temp.Substring(0, Math.Min(temp.Length, 60)) + "..."));
                        }

                        var cmd = parse.Substring(0, colon);
                        var part = parse.Substring(colon + 1);

                        var save = cmd.Contains("?");
                        var nextToken = cmd.Contains("&");
                        var prefixed = cmd.Contains("+");
                        var restore = cmd.Contains("^");
                        var bracketed = cmd.Contains("[");
                        var executeAnyway = cmd.Contains(">");
                        var endIftrue = cmd.Contains(".");
                        var resetLogicalToNoValue = cmd.Contains("<");
                        var isWildcard = cmd.Contains("*");

                        // todo: 
                        // N:|XX:/roadclosures/item[contains(community,'{city}')]

                        // G[:{X:item/status},{X:item/location}]
                        if (string.IsNullOrWhiteSpace(temp) && !executeAnyway)
                        {
                            if (IsDebug)
                            {
                                DebugString.AppendLine(">>empty: skipping " + parse);
                            }

                            continue;
                        }

                        if (nextToken)
                        {
                            results.Add(temp);
                            temp = string.Empty;
                        }

                        if (prefixed)
                        {
                            prefix += temp;
                            temp = string.Empty;
                        }

                        if (restore)
                        {
                            temp = potentialResults.Count > 0 ? potentialResults.Pop() : Content;
                        }

                        if (save)
                        {
                            potentialResults.Push(prefix + temp);
                            prefix = string.Empty;
                        }

                        var contains = temp?.Contains(part);

                        if (cmd.Contains("$"))
                        {
                            if (CustomParser != null)
                            {
                                temp = CustomParser
                                .Invoke(
                                    this,
                                    new CustomParserArguments
                                    {
                                        Current = temp,
                                        Instruction = part,
                                        Command = cmd,
                                        Content = Content
                                    });
                            }
                        }
                        else if (cmd.Contains("i"))
                        {
                            if (!CheckDoc(temp))
                            {
                                return false;
                            }

                            nodes = doc.SelectNodes(part);
                            IsDictionary = true;
                        }
                        else if (cmd.Contains("AQ"))
                        {
                            // AngleSharp QuerySelector
                            var parser = new HtmlParser();
                            var document = parser.ParseDocument(temp);
                            temp = document.QuerySelector(part)?.TextContent;
                        }
                        else if (cmd.Contains("k"))
                        {
                            keypart = part;
                        }
                        else if (cmd.Contains("v"))
                        {
                            valuepart = part;
                        }
                        else if (cmd.Contains("V")) //validate part so far - if empty, missing data
                        {
                            if (string.IsNullOrEmpty(temp))
                            {
                                // missing data
                                if (IsDebug)
                                {
                                    DebugString.AppendLine();
                                    DebugString.AppendLine("Validated as empty");
                                }

                                return false;
                            }
                        }
                        else if (cmd.Contains("table") && nodes != null)
                        {
                            if (part.Contains("name="))
                            {
                                Tablename = part.Substring(part.IndexOf("name=") + 5);
                                var j = Tablename.IndexOf(";");
                                if (j > -1)
                                {
                                    Tablename = Tablename.Substring(0, j);
                                }
                            }

                            foreach (XmlNode node in nodes)
                            {
                                XmlNode key = null;
                                XmlNodeList valueset = null;
                                try
                                {
                                    key = node.SelectSingleNode(keypart);
                                    valueset = node.SelectNodes(valuepart);
                                }
                                catch (Exception)
                                {
                                    // skip if not matching
                                    continue;
                                }

                                if (!string.IsNullOrWhiteSpace(key?.InnerText))
                                {
                                    if (!string.IsNullOrWhiteSpace(key.InnerText) &&
                                        !Dictionary.ContainsKey(key.InnerText))
                                    {
                                        var values = new StringBuilder();
                                        var count = valueset.Count;
                                        foreach (XmlNode value in valueset)
                                        {
                                            values.Append(value.InnerText);
                                            if (--count > 0)
                                            {
                                                values.Append("~");
                                            }
                                        }

                                        Dictionary.Add(key.InnerText, values.ToString());
                                    }
                                }
                            }

                            Debug.WriteLine("Table {0} saved count = {1}", Tablename, Dictionary.Count);
                        }
                        else if (cmd.Contains("TEXT"))
                        {
                            var len = 0;
                            while (temp.Contains("<") && temp.Length != len)
                            {
                                len = temp.Length;
                                var word = temp.Between("<", ">");
                                temp = temp.Replace("<" + word + ">", string.Empty);
                            }
                        }
                        else if (cmd.Contains("R"))
                        {
                            temp = cmd.Contains("Rl") ? temp.RightOfLast(part) : temp.RightOf(part, cmd.Contains("RI"));
                        }
                        else if (cmd.Contains("T"))
                        {
                            temp = temp.Trim();
                        }
                        else if (cmd.Contains("L"))
                        {
                            temp = temp.LeftOf(part, cmd.Contains("LI"));
                        }
                        else if (cmd.Contains("B"))
                        {
                            temp = temp.Between(part);
                        }
                        else if (cmd.Contains("D"))
                        {
                            temp = HttpUtility.HtmlDecode(temp);
                        }
                        else if (cmd.Contains("N"))
                        {
                            temp = temp.Replace("  ", " ").Replace("  ", " ");
                        }
                        else if (cmd.Contains("S"))
                        {
                            // terciary switch based on previous logical result
                            var parts2 = part.Split('~');
                            var cmdparts2 = new List<string>() { string.Empty, string.Empty };

                            var previousTemp = temp;

                            // context switch?
                            var hasKeys = false;
                            var index = 0;
                            foreach (var singlepart in parts2)
                            {
                                if (singlepart.Length > 0 && singlepart.StartsWith("@"))
                                {
                                    cmdparts2[index] = singlepart.Substring(1);
                                }

                                var equals = singlepart.IndexOf("=");
                                if (equals > -1)
                                {
                                    hasKeys = true;
                                    var keyvalue = singlepart.Split('=');
                                    if (keyvalue[0].Equals(temp) ||
                                        (keyvalue[0].Equals("()") && string.IsNullOrWhiteSpace(temp)) ||
                                        keyvalue[0].Equals("*"))
                                    {
                                        temp = keyvalue[1];
                                        break;
                                    }
                                }

                                ++index;
                            }

                            var expectedIndex = LogicalResult.HasValue && LogicalResult.Value ? 0 : 1;

                            if (!hasKeys)
                            {
                                if (parts2.Length > expectedIndex)
                                {
                                    temp = parts2[expectedIndex].Equals("*") ? temp : parts2[expectedIndex];

                                    if (endIftrue)
                                    {
                                        break;
                                    }
                                }
                            }

                            if (endIftrue)
                            {
                                LogicalResult = null;
                            }

                            if (temp == "()")
                            {
                                temp = string.Empty;
                            }

                            if (!string.IsNullOrWhiteSpace(cmdparts2[expectedIndex]))
                            {
                                temp = previousTemp;
                                parse = cmdparts2[expectedIndex];
                                isRepeat = true;

                                afterIdentified = afterIdentified || SetAfter(temp);

                                continue;
                            }
                        }
                        else if (cmd.Contains("C") || cmd.Contains("c"))
                        {
                            var test = false;
                            if (bracketed)
                            {
                                var part2 = part.LeftOf("]");
                                var parts2 = part2.Split('~');
                                foreach (var parts2part in parts2)
                                {
                                    test = test ||
                                        (isWildcard
                                            ? temp.EqualsWildcard(parts2part) || temp.ContainsWildcard(parts2part)
                                            : temp.Contains(parts2part));
                                }
                            }
                            else
                            {
                                test = part.Equals("*")
                                    ? !string.IsNullOrWhiteSpace(temp)
                                    : (isWildcard
                                        ? temp.EqualsWildcard(part) || temp.ContainsWildcard(part)
                                        : temp.Contains(part));
                            }

                            if (resetLogicalToNoValue)
                            {
                                LogicalResult = null;
                            }

                            LogicalResult = LogicalResult.HasValue ? (LogicalResult.Value && test) : test;

                            if (cmd.Contains("c"))
                            {
                                LogicalResult = !LogicalResult;
                            }
                        }
                        else if (cmd.Contains("!"))
                        {
                            temp = part;
                        }
                        else if (cmd.Contains("J") || cmd.Contains("j"))
                        {
                            JToken jParsed;
                            try
                            {
                                jParsed = JToken.Parse(temp);
                            }
                            catch (Exception)
                            {
                                Debug.WriteLine("EXCEPTION: Failed to JParse: " + temp);
                                return false;
                            }

                            if (jParsed != null)
                            {
                                var jToken = jParsed.SelectToken(part);
                                if (jToken != null)
                                {
                                    if (jToken is JArray)
                                    {
                                        ResultArray = jToken as JArray;
                                        temp = JsonConvert.SerializeObject(jToken);
                                    }
                                    else
                                    {
                                        if (cmd.Contains("j"))
                                        {
                                            temp = ((JProperty)jToken).Name;
                                            if (cmd.Contains("J"))
                                            {
                                                temp += ":'" + jToken.Value<string>() + "'";
                                            }
                                        }
                                        else
                                        {
                                            temp = jToken.Value<string>();
                                        }
                                    }
                                }
                                else
                                {
                                    temp = string.Empty;
                                }
                            }
                            else
                            {
                                temp = string.Empty;
                            }
                        }
                        else if (cmd.Contains("X") || cmd.Contains("x") || cmd.Contains("Q") || cmd.Contains("q")) //q=limited jquery translation (#ids and .classes)
                        {
                            // xpath
                            try
                            {
                                doc = LoadDoc(temp);
                            }
                            catch (XmlException)
                            {
                                doc = LoadDoc("<root>" + temp + "</root>");
                            }

                            if (doc == null)
                            {
                                throw new Exception("doc (X:) is null");
                            }

                            var nsmgr = new XmlNamespaceManager(doc.NameTable);

                            if (cmd.Contains("Q") || cmd.Contains("q"))
                            {
                                if (part.StartsWith("."))
                                {
                                    part = part.Substring(1);
                                    part = $".//*[contains(@class,'{part}')]";
                                }
                                else if (part.StartsWith("#"))
                                {
                                    part = part.Substring(1);
                                    part = $".//*[@id='{part}']";
                                }
                            }

                            if (cmd.Contains("XX") || cmd.Contains("QQ"))
                            {
                                var xnodes2 = doc.SelectNodes(part);
                                if (xnodes2.Count > 0)
                                {
                                    var result = new StringBuilder();
                                    result.Append("<root>");
                                    foreach (XmlNode xnode2 in xnodes2)
                                    {
                                        // result.Append( "<" + xnode2.Name + ">" );
                                        result.Append(xnode2.OuterXml);

                                        // result.Append( "</" + xnode2.Name + ">" );
                                    }

                                    result.Append("</root>");
                                    temp = result.ToString();
                                }
                            }
                            else
                            {
                                var xnodes = doc.SelectSingleNode(part);
                                temp = cmd.Contains("x") || cmd.Contains("q") ? xnodes?.InnerText : xnodes?.InnerXml;  // was R:call-out-text-danger|R:Safety Burn Ban|R:Status:|L:
                            }
                        }
                        else if (cmd.Contains("E"))
                        {
                            // regex
                            var expr = new Regex(part);
                            var matches = expr.Match(part);
                            var matchResults = new StringBuilder();
                            while (matches.Success)
                            {
                                contains = true;
                                matchResults.Append(matches.Value);
                                matchResults.Append("~");
                                matches = matches.NextMatch();
                            }

                            temp = matchResults.ToString();
                            if (!string.IsNullOrWhiteSpace(temp))
                            {
                                temp = temp.Substring(0, temp.Length - 1);
                            }
                        }
                        else if (cmd.Contains("F"))
                        {
                            results.Add(prefix + temp);
                            prefix = string.Empty;
                            temp = string.Format(part, results.ToArray());
                            results.Clear();
                        }
                        else if (cmd.Contains("G"))
                        {
                            // G:X:root/item[{X:status},{X:location}]
                            if (!part.StartsWith("X:"))
                            {
                                continue;
                            }

                            var fullpart = part + (remainingParses.Length > 0 ? "|" : string.Empty) + remainingParses;

                            fullpart = fullpart.Substring(2);

                            var leftarraychar = '<';
                            var rightarraychar = '>';

                            // WIP HERE
                            var extra = fullpart.IndexOf(rightarraychar) == fullpart.Length - 1
                                ? string.Empty
                                : "|" + fullpart.Substring(fullpart.IndexOf(rightarraychar) + 1);
                            var bracks = fullpart.Split(leftarraychar, rightarraychar);
                            var doc2 = LoadDoc(temp);
                            var docnodes = doc2.SelectNodes(bracks[0]);

                            var subparts = bracks[1].Split(',');

                            var parser2 = new PatternParser();
                            parser2.CustomParser = CustomParser;
                            var rows = new List<List<string>>();
                            foreach (XmlNode docnode in docnodes)
                            {
                                var cols = new List<string>();
                                foreach (var subpart in subparts)
                                {
                                    parser2.Content =
                                    docnode.OuterXml
                                        .Replace("<" + docnode.Name + " ", "<r ")
                                        .Replace("<" + docnode.Name + ">", "<r>")
                                        .Replace("</" + docnode.Name + ">", "</r>");
                                    parser2.Instructions = subpart.Substring(1, subpart.Length - 2) +
                                        (extra.Length > 0 ? "|" : string.Empty) +
                                        extra;
                                    if (parser2.Parse())
                                    {
                                        cols.Add(parser2.Result);
                                    }
                                }

                                rows.Add(cols);
                            }

                            temp = JsonConvert.SerializeObject(rows);

                            breakTop = true;
                            break;
                        }

                        if (save && (contains.HasValue && !contains.Value))
                        {
                            temp = potentialResults.Pop();
                        }
                    }

                    afterIdentified = afterIdentified || SetAfter(temp);
                }

                afterIdentified = afterIdentified || SetAfter(temp);

                if (breakTop)
                {
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(temp))
            {
                results.Add(prefix + temp);
                prefix = string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < results.Count; i++)
            {
                builder.Append(results[i]);
                if (i < results.Count - 1)
                {
                    builder.Append("|");
                }
            }

            Result = builder.Length == 0 ? string.Empty : builder.ToString();

            return true;
        }

        private bool SetAfter(string temp)
        {
            if (IsDebug)
            {
                var step = DebugSteps.LastOrDefault();
                if (step != null)
                {
                    if (temp == step.BeforeContent)
                    {
                        step.IsIdentical = true;
                    }
                    else
                    {
                        step.AfterContent = temp;
                    }

                    step.LogicalResult = LogicalResult;

                    DebugString
                        .AppendLine(
                            "After:" +
                                (string.IsNullOrWhiteSpace(temp)
                                    ? string.Empty
                                    : temp.Substring(0, Math.Min(temp.Length, 60)) + "..."));
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the ancestor node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="condition">The condition to return the node.</param>
        /// <returns>AngleSharp.Html.Dom.HtmlElement.</returns>
        private AngleSharp.Html.Dom.HtmlElement GetAncestor(
            AngleSharp.Html.Dom.HtmlElement node,
            Func<AngleSharp.Html.Dom.HtmlElement, bool> condition)
        {
            var element = node;
            while (element != null)
            {
                if (condition(element))
                {
                    return element;
                }

                element = element.ParentElement as AngleSharp.Html.Dom.HtmlElement;
            }

            return null;
        }

        /// <summary>
        /// Creates the recipe, or an attempted instruction to find the goal in the content.
        /// </summary>
        /// <param name="goal">The goal.</param>
        /// <returns>System.String.</returns>
        public string CreateRecipe(string goal)
        {
            var parser = new HtmlParser();
            var document = parser.ParseDocument(Content);
            var node = document.All
                .Where(m => m.TextContent.Contains(goal))
                .OrderBy(m => m.TextContent.Length)
                .FirstOrDefault() as AngleSharp.Html.Dom.HtmlElement;

            var step = 0;
            var q = string.Empty;

            if (node != null)
            {
                var bestId = GetAncestor(node, m => m.Id != null);
                if (bestId != null)
                {
                    q += "#" + bestId.Id;
                }

                if (node.TextContent.Length == bestId.TextContent.Length)
                {
                    // good enough!
                    ++step;

                    // go to text!
                }

                if (step == 0)
                {
                    var less = int.MaxValue;
                    var element = node;

                    while (element != null)
                    {
                        var firstClassList = GetAncestor(element, m => m.ClassList != null);
                        var lessClass = string.Empty;
                        for (int i = 0; i < firstClassList.ClassList.Length; i++)
                        {
                            var classItem = firstClassList.ClassList[i];
                            var nodes = document.QuerySelectorAll(q + ">>." + classItem);
                            if (nodes.Length > 0 && nodes.Length < less)
                            {
                                less = nodes.Length;
                                lessClass = classItem;
                            }

                            // todo:if text not the same, continue adding classitems to isolate
                        }

                        if (less == 1)
                        {
                            q += ">>." + lessClass;
                            step++;
                            break;
                        }

                        element = element.ParentElement as AngleSharp.Html.Dom.HtmlElement;
                    }
                }

                if (step == 0)
                {
                    return null;
                }

                var inst = "AQ:" + q;

                // text!
                var element2 = document.QuerySelector(q);
                var text = element2.TextContent;
                if (text.Length == goal.Length)
                {
                    return inst;
                }

                inst += "|T:";
                text = text.Trim();
                if (text.Length == goal.Length)
                {
                    return inst;
                }

                var left = text.IndexOf(goal);
                if (left > 0)
                {
                    for (int i = left - 1; i >= 0; i--)
                    {
                        if (text.IndexOf(text.Substring(i, left - i)) == i)
                        {
                            inst += "|R:" + text.Substring(i, left - i);
                            text = text.Substring(left);
                            break;
                        }
                    }
                }

                if (text.Length > goal.Length)
                {
                    for (int i = goal.Length; i < text.Length; i++)
                    {
                        if (text.IndexOf(text.Substring(goal.Length, i - goal.Length + 1)) == goal.Length)
                        {
                            inst += "|L:" + text.Substring(goal.Length, i - goal.Length + 1);
                            text = text.Substring(0, goal.Length);
                            break;
                        }
                    }
                }

                return inst;
            }

            return null;
        }

        /// <summary>
        /// Checks the document.
        /// </summary>
        /// <param name="xmlOrHtml">The xml or html string.</param>
        /// <returns><c>true</c> if valid, <c>false</c> otherwise.</returns>
        public bool CheckDoc(string xmlOrHtml)
        {
            if (doc != null)
            {
                return true;
            }

            try
            {
                doc = new XmlDocument();
                doc.LoadXml(xmlOrHtml);
            }
            catch (Exception)
            {
                // backup leverage html agility pack
                try
                {
                    var hdoc = new HtmlDocument();
                    hdoc.LoadHtml(xmlOrHtml);
                    hdoc.OptionOutputAsXml = true;
                    hdoc.OptionAutoCloseOnEnd = true;

                    var stream = new MemoryStream();

                    var xtw = XmlWriter.Create(
                        stream,
                        new XmlWriterSettings
                        { ConformanceLevel = ConformanceLevel.Fragment });

                    hdoc.Save(xtw);

                    stream.Position = 0;

                    doc.LoadXml((new StreamReader(stream)).ReadToEnd());
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Loads the document.
        /// </summary>
        /// <param name="xmlOrHtml">The xml or html string.</param>
        /// <returns>XmlDocument.</returns>
        public XmlDocument LoadDoc(string xmlOrHtml)
        {
            var doc = new XmlDocument();

            try
            {
                doc.LoadXml(xmlOrHtml);
            }
            catch (Exception e)
            {
                // backup leverage html agility pack
                var tryagain = true;
                if (e.Message.Contains("There are multiple root elements"))
                {
                    xmlOrHtml = "<root>" + xmlOrHtml + "</root>";
                    try
                    {
                        doc.LoadXml(xmlOrHtml);
                        tryagain = false;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (tryagain)
                {
                    try
                    {
                        // FIX UP COLONS IN NAMES
                        xmlOrHtml = xmlOrHtml.Replace("xlink:", "xlink_").Replace("xmlns:", "xmlns_");

                        var hdoc = new HtmlDocument();
                        hdoc.LoadHtml(xmlOrHtml);
                        hdoc.OptionOutputAsXml = true;
                        hdoc.OptionAutoCloseOnEnd = true;

                        var stream = new MemoryStream();

                        var xtw = XmlWriter.Create(
                            stream,
                            new XmlWriterSettings
                            { ConformanceLevel = ConformanceLevel.Fragment });

                        hdoc.Save(xtw);

                        stream.Position = 0;

                        doc.LoadXml((new StreamReader(stream)).ReadToEnd());
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }

            return doc;
        }
    }

    public class DebugStep
    {
        public int Step { get; set; }

        public string BeforeContent { get; set; }

        private string afterContext { get; set; }

        public string AfterContent
        {
            get { return IsIdentical ? BeforeContent : afterContext; }
            set { afterContext = value; }
        }

        public string Instruction { get; set; }

        public bool IsIdentical { get; set; }

        public bool? LogicalResult { get; set; }
    }
}
