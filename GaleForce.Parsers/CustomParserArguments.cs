//-----------------------------------------------------------------------
// <copyright file="CustomParserArguments.cs" company="Jim Gale">
// Copyright (C) Jim Gale. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace GaleForceCore.Parsers
{
    /// <summary>
    /// Class CustomParserArguments.
    /// </summary>
    public class CustomParserArguments
    {
        /// <summary>
        /// Gets or sets the current parse result.
        /// </summary>
        public string Current { get; set; }

        /// <summary>
        /// Gets or sets the instruction.
        /// </summary>
        public string Instruction { get; set; }

        /// <summary>
        /// Gets or sets the command.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; }
    }
}
