﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Formatting.Rules
{
    internal interface IBaseIndentationFormattingRuleFactoryService : IWorkspaceService
    {
        bool ShouldUseBaseIndentation(Document document);
        IFormattingRule CreateRule(Document document, int position);
        IEnumerable<TextChange> FilterFormattedChanges(Document document, TextSpan span, IList<TextChange> changes);
    }
}
