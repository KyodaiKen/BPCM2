﻿using System;

namespace NAudioLitle.Utils
{
    /// <summary>
    /// Allows us to add descriptions to interop members
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FieldDescriptionAttribute : Attribute
    {
        /// <summary>
        /// The description
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Field description
        /// </summary>
        public FieldDescriptionAttribute(string description)
        {
            Description = description;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Description;
        }
    }
}