﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Umbraco.Core.Models;

namespace Our.Umbraco.Migration.DataTypeMigrators
{
    [DataTypeMigrator("Umbraco.MultiNodeTreePicker")]
    public class MultiNodeTreePickerMigrator : IdToUdiMigrator
    {
        private static readonly Regex MediaTypePattern = new Regex("\"type\"\\s*:\\s*\"media\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override string GetNewPropertyEditorAlias(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) => "Umbraco.MultiNodeTreePicker2";

        public override ContentBaseType GetNewPropertyContentBaseType(IDataTypeDefinition dataType, IDictionary<string, PreValue> oldPreValues) =>
            oldPreValues != null && oldPreValues.TryGetValue("startNode", out var value) && value.Value != null && MediaTypePattern.IsMatch(value.Value)
            ? ContentBaseType.Media
            : ContentBaseType.Document;
    }
}
