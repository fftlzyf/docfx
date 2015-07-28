// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel
{
    using System.Collections.Generic;
    using YamlDotNet.Serialization;

    public class SyntaxDetail
    {
        [YamlMember(Alias = "content")]
        public SortedList<SyntaxLanguage, string> Content { get; set; }

        [YamlMember(Alias = "parameters")]
        public List<ApiParameter> Parameters { get; set; }

        [YamlMember(Alias = "typeParameters")]
        public List<ApiParameter> TypeParameters { get; set; }

        [YamlMember(Alias = "return")]
        public ApiParameter Return { get; set; }
    }
}
