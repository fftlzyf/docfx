// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.Engine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Xunit;

    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.Tests.Common;

    [Trait("Owner", "lianwei")]
    [Trait("EntityType", "TemplateManager")]
    public class TemplateManagerUnitTest : TestBase
    {
        private readonly string _inputFolder;
        private readonly string _outputFolder;

        public TemplateManagerUnitTest()
        {
            _inputFolder = GetRandomFolder();
            _outputFolder = GetRandomFolder();
        }

        [Trait("Related", "ResourceFinder")]
        [Fact]
        public void TestResourceFinderFromAssembly()
        {
            var testFinder = new ResourceFinder(this.GetType().Assembly, "tmpl");

            // 1. Support tmpl1.zip
            using (var result = testFinder.Find("tmpl1"))
            {
                Assert.NotNull(result);
                Assert.Equal(2, result.Names.Count());
                var item = result.GetResource("tmpl1.dot.$");
                Assert.Equal("This is file with complex filename characters", item);

                // backslash is also supported
                item = result.GetResource(@"sub\file1");
                Assert.Equal("This is file inside a subfolder", item);
            }
        }

        [Trait("Related", "ResourceFinder")]
        [Fact]
        public void TestTemplateManagerWithMutipleThemesShouldWork()
        {
            // If the same resource name exists in the override folder, use the overriden one
            var themes = new List<string> { "tmpl1", "tmpl/tmpl1" };
            var manager = new TemplateManager(GetType().Assembly, "tmpl", null, themes, null);
            var outputFolder = Path.Combine(_outputFolder, "TestTemplateManager_MutipleThemes");
            manager.ProcessTheme(outputFolder, true);
            // 1. Support tmpl1.zip
            var file1 = Path.Combine(outputFolder, "tmpl1.dot.$");
            Assert.True(File.Exists(file1));
            Assert.Equal("Override: This is file with complex filename characters", File.ReadAllText(file1));

            // backslash is also supported
            var file2 = Path.Combine(outputFolder, "sub/file1");
            Assert.True(File.Exists(file2));
            Assert.Equal("Override: This is file inside a subfolder", File.ReadAllText(file2));
        }

        #region Mustache template processor test

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Mustache")]
        [Fact]
        public void TestMustacheTemplateProcessSingleTemplateWithNoScriptShouldWork()
        {
            // 1. Prepare template
            var templateName = "NoScript";
            string template = @"
{{#model}}
name1={{name1}},
name2={{name2}};
{{/model}}
";
            var model = new
            {
                model = new object[]
               {
                   new {name1 = "test1"},
                   new {name2 = "test2"},
               }
            };
            var modelFileName = Path.Combine(_inputFolder, "TestTemplateProcessor_NoScript.yml");
            var item = new InternalManifestItem { DocumentType = string.Empty, Key = modelFileName, FileWithoutExtension = Path.ChangeExtension(modelFileName, null), ResourceFile = modelFileName };
            ProcessTemplate(templateName, null, new[] { item }, model, _outputFolder, Tuple.Create("default.tmpl", template));

            var outputFile = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, string.Empty));
            Assert.True(File.Exists(outputFile));
            Assert.Equal(@"
name1=test1,
name2=;
name1=,
name2=test2;
", File.ReadAllText(outputFile));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Mustache")]
        [Fact]
        public void TestMustacheTemplateProcessSingleTemplateWithNoScriptWithPartialShouldWork()
        {
            // 1. Prepare template
            var templateName = "NoScriptWithPartial";
            string template = @"
{{#model}}
{{name}}
{{/model}}
{{>partial1}}
";
            string partial1 = @"partial 1:
{{>partial2}}";
            string partial2 = @"partial 2:
{{#model}}
{{name}}
{{/model}}
";
            var model = new
            {
                model = new[]
               {
                   new {name = "test1"},
                   new {name = "test2"},
               }
            };
            var modelFileName = Path.Combine(_inputFolder, "TestTemplateProcessor_NoScriptWithPartial.yml");
            var item = new InternalManifestItem { DocumentType = string.Empty, Key = modelFileName, FileWithoutExtension = Path.ChangeExtension(modelFileName, null), ResourceFile = modelFileName };
            ProcessTemplate(
                templateName,
                null,
                new[] { item },
                model,
                _outputFolder,
                Tuple.Create("default.tmpl", template),
                Tuple.Create("partial1.tmpl.partial", partial1),
                Tuple.Create("partial2.tmpl.partial", partial2));

            var outputFile = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, string.Empty));
            Assert.True(File.Exists(outputFile));
            Assert.Equal(@"
test1
test2
partial 1:
partial 2:
test1
test2
", File.ReadAllText(outputFile));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Mustache")]
        [Fact]
        public void TestMustacheTemplateProcessInvalidTemplateShouldFail()
        {
            var templateName = "InvalidTemplate.html";
            string inputFolder = null;
            var modelFileName = Path.Combine(_inputFolder, "TestTemplateProcessor_InvalidTemplate.yml");
            var item = new InternalManifestItem { FileWithoutExtension = Path.ChangeExtension(modelFileName, null), DocumentType = string.Empty, Key = modelFileName };
            ProcessTemplate(templateName, inputFolder, new[] { item }, new object(), _outputFolder,
                Tuple.Create("default.invalidtmpl", string.Empty),
                Tuple.Create("default.js", string.Empty),
                Tuple.Create("reference1.html", string.Empty),
                Tuple.Create("reference2.html", string.Empty)
                );
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Mustache")]
        [Fact]
        public void TestMustacheTemplateProcessSingleTemplateWithIncludesShouldWork()
        {
            var templateName = "WithIncludes.html";

            string template = @"
{{ !include('reference1.html') }}
{{ !include('reference2.html') }}
{{#model}}
{{name}}
{{/model}}
";
            string script = @"
exports.transform = function (model){
    model.model.push({name:'test2'});
    return model;
}";

            var model = new
            {
                model = new List<object>
               {
                   new {name = "test1"},
               }
            };

            var modelFileName = Path.Combine(_inputFolder, "TestTemplateProcessor_WithIncludes.yml");
            string inputFolder = null;
            var item = new InternalManifestItem { FileWithoutExtension = Path.ChangeExtension(modelFileName, null), DocumentType = string.Empty, Key = modelFileName };
            ProcessTemplate(templateName, inputFolder, new[] { item }, model, _outputFolder,
                Tuple.Create("default.html.tmpl", template),
                Tuple.Create("default.html.js", script),
                Tuple.Create("reference1.html", string.Empty),
                Tuple.Create("reference2.html", string.Empty)
                );
            var outputFilePath = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, "html"));
            Assert.True(File.Exists(outputFilePath));
            Assert.True(File.Exists(Path.Combine(_outputFolder, "reference1.html")));
            Assert.True(File.Exists(Path.Combine(_outputFolder, "reference2.html")));
            Assert.Equal(@"
test1
test2
", File.ReadAllText(outputFilePath));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Mustache")]
        [Fact]
        public void TestMustacheTemplateProcessTemplateFolderWithDifferentTypeShouldWork()
        {
            var templateName = "TemplateFolder.html";
            string defaultTemplate = @"
default:
{{#model}}
{{name}}
{{/model}}
";
            string conceptualTemplate = @"
conceptual:
{{#model}}
{{name}}
{{/model}}
";
            string script = @"
exports.transform = function (model){
    model.model.push({name:'test2'});
    return model;
}";

            var model = new
            {
                model = new List<object>
               {
                   new {name = "test1"},
               }
            };

            string inputFolder = null;
            var item1 = new InternalManifestItem { FileWithoutExtension = "TestTemplateProcessor_TemplateFolderWithDifferentType1", Key = "x.yml", DocumentType = "Conceptual" };
            var item2 = new InternalManifestItem { DocumentType = string.Empty, FileWithoutExtension = "TestTemplateProcessor_TemplateFolderWithDifferentType2", Key = "y.yml" };
            ProcessTemplate(templateName, inputFolder, new[] { item1, item2 }, model, _outputFolder,
                Tuple.Create("default.html.tmpl", defaultTemplate),
                Tuple.Create($"{templateName}/conceptual.md.tmpl", conceptualTemplate),
                Tuple.Create("default.html.js", script),
                Tuple.Create("conceptual.md.js", script)
                );
            var outputFilePath1 = Path.Combine(_outputFolder, "TestTemplateProcessor_TemplateFolderWithDifferentType1.md");
            Assert.True(File.Exists(outputFilePath1));
            Assert.Equal(@"
conceptual:
test1
test2
", File.ReadAllText(outputFilePath1));
            var outputFilePath2 = Path.Combine(_outputFolder, "TestTemplateProcessor_TemplateFolderWithDifferentType2.html");
            Assert.True(File.Exists(outputFilePath2));
            Assert.Equal(@"
default:
test1
test2
", File.ReadAllText(outputFilePath2));
        }

        #endregion

        #region Liquid template processor test

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Liquid")]
        [Fact]
        public void TestLiquidTemplateProcessSingleTemplateWithNoScriptShouldWork()
        {
            // 1. Prepare template
            var templateName = "NoScript.liquid";
            string template = @"
{% for item in model -%}
{{ item.name }}
{% endfor -%}
";
            var model = new
            {
                model = new[]
               {
                   new {name = "test1"},
                   new {name = "test2"},
               }
            };
            var modelFileName = Path.Combine(_inputFolder, "TestLiquidTemplateProcessor_NoScript.yml");
            var item = new InternalManifestItem { DocumentType = string.Empty, Key = modelFileName, FileWithoutExtension = Path.ChangeExtension(modelFileName, null), ResourceFile = modelFileName };
            ProcessTemplate(templateName, null, new[] { item }, model, _outputFolder, Tuple.Create("default.liquid", template));

            var outputFile = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, string.Empty));
            Assert.True(File.Exists(outputFile));
            Assert.Equal(@"
test1
test2
", File.ReadAllText(outputFile));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Liquid")]
        [Fact]
        public void TestLiquidTemplateProcessSingleTemplateWithNoScriptWithIncludeShouldWork()
        {
            // 1. Prepare template
            var templateName = "NoScriptWithInclude.liquid";
            string template = @"
{% for item in model -%}
{{ item.name }}
{% endfor -%}
{% include partial1 -%}
";
            string partial1 = @"partial 1:
{% include partial2 -%}";
            string partial2 = @"partial 2:
{% for item in model -%}
{{ item.name }}
{% endfor -%}
";
            var model = new
            {
                model = new[]
               {
                   new {name = "test1"},
                   new {name = "test2"},
               }
            };
            var modelFileName = Path.Combine(_inputFolder, "TestLiquidTemplateProcessor_NoScriptWithPartial.yml");
            var item = new InternalManifestItem { DocumentType = string.Empty, Key = modelFileName, FileWithoutExtension = Path.ChangeExtension(modelFileName, null), ResourceFile = modelFileName };
            ProcessTemplate(
                templateName,
                null,
                new[] { item },
                model,
                _outputFolder,
                Tuple.Create("default.liquid", template),
                Tuple.Create("_partial1.liquid", partial1),
                Tuple.Create("_partial2.liquid", partial2));

            var outputFile = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, string.Empty));
            Assert.True(File.Exists(outputFile));
            Assert.Equal(@"
test1
test2
partial 1:
partial 2:
test1
test2
", File.ReadAllText(outputFile));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Liquid")]
        [Fact]
        public void TestLiquidTemplateProcessSingleTemplateWithDependenciesShouldWork()
        {
            var templateName = "WithIncludes.liquid";

            string template = @"
{% ref reference1.html -%}
{% ref reference2.html -%}
{% for item in model -%}
{{ item.name }}
{% endfor -%}
";
            string script = @"
exports.transform = function (model){
    model.model.push({name:'test2'});
    return model;
}";

            var model = new
            {
                model = new List<object>
               {
                   new {name = "test1"},
               }
            };

            var modelFileName = Path.Combine(_inputFolder, "TestLiquidTemplateProcessor_WithIncludes.yml");
            string inputFolder = null;
            var item = new InternalManifestItem { FileWithoutExtension = Path.ChangeExtension(modelFileName, null), DocumentType = string.Empty, Key = modelFileName };
            ProcessTemplate(templateName, inputFolder, new[] { item }, model, _outputFolder,
                Tuple.Create("default.html.liquid", template),
                Tuple.Create("default.html.js", script),
                Tuple.Create("reference1.html", string.Empty),
                Tuple.Create("reference2.html", string.Empty)
                );
            var outputFilePath = Path.Combine(_outputFolder, Path.ChangeExtension(modelFileName, "html"));
            Assert.True(File.Exists(outputFilePath));
            Assert.True(File.Exists(Path.Combine(_outputFolder, "reference1.html")));
            Assert.True(File.Exists(Path.Combine(_outputFolder, "reference2.html")));
            Assert.Equal(@"
test1
test2
", File.ReadAllText(outputFilePath));
        }

        [Trait("Related", "TemplateProcessor")]
        [Trait("Related", "Liquid")]
        [Fact]
        public void TestLiquidTemplateProcessTemplateFolderWithDifferentTypeShouldWork()
        {
            var templateName = "TemplateFolder.liquid";
            string defaultTemplate = @"
default:
{% for item in model -%}
{{ item.name }}
{% endfor -%}
";
            string conceptualTemplate = @"
conceptual:
{% for item in model -%}
{{ item.name }}
{% endfor -%}
";
            string script = @"
exports.transform = function (model){
    model.model.push({name:'test2'});
    return model;
}";

            var model = new
            {
                model = new List<object>
               {
                   new {name = "test1"},
               },
                another = new Dictionary<string, object>
                {
                    ["key1"] = new { name = "test3" },
                    ["key2"] = new { name = "test4" }
                }
            };

            string inputFolder = null;
            var item1 = new InternalManifestItem { FileWithoutExtension = "TestLiquidTemplateProcessor_TemplateFolderWithDifferentType1", Key = "x.yml", DocumentType = "Conceptual" };
            var item2 = new InternalManifestItem { DocumentType = string.Empty, FileWithoutExtension = "TestLiquidTemplateProcessor_TemplateFolderWithDifferentType2", Key = "y.yml" };
            ProcessTemplate(templateName, inputFolder, new[] { item1, item2 }, model, _outputFolder,
                Tuple.Create("default.html.liquid", defaultTemplate),
                Tuple.Create($"{templateName}/conceptual.md.liquid", conceptualTemplate),
                Tuple.Create("default.html.js", script),
                Tuple.Create("conceptual.md.js", script)
                );
            var outputFilePath1 = Path.Combine(_outputFolder, "TestLiquidTemplateProcessor_TemplateFolderWithDifferentType1.md");
            Assert.True(File.Exists(outputFilePath1));
            Assert.Equal(@"
conceptual:
test1
test2
", File.ReadAllText(outputFilePath1));
            var outputFilePath2 = Path.Combine(_outputFolder, "TestLiquidTemplateProcessor_TemplateFolderWithDifferentType2.html");
            Assert.True(File.Exists(outputFilePath2));
            Assert.Equal(@"
default:
test1
test2
", File.ReadAllText(outputFilePath2));
        }

        #endregion

        private static void ProcessTemplate(string templateName, string inputFolder, IEnumerable<InternalManifestItem> items, object model, string outputFolder, params Tuple<string, string>[] templateFiles)
        {
            var rootTemplateFolder = "tmpl";
            var templateFolder = Path.Combine(rootTemplateFolder, templateName);
            if (Directory.Exists(templateFolder))
                Directory.Delete(templateFolder, true);
            WriteTemplate(templateFolder, templateFiles);
            using (var resource = new ResourceFinder(null, null).Find(templateFolder))
            {
                var processor = new TemplateProcessor(resource, null, 4);
                var context = new DocumentBuildContext(inputFolder);
                foreach (var item in items)
                {
                    if (item.ResourceFile != null)
                    {
                        var dir = Path.GetDirectoryName(item.ResourceFile);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.Create(item.ResourceFile).Dispose();
                    }
                    if (string.IsNullOrEmpty(item.InputFolder)) item.InputFolder = Directory.GetCurrentDirectory();
                    item.Model = new DocAsCode.Plugins.ModelWithCache(model);
                }
                var settings = new ApplyTemplateSettings(inputFolder, outputFolder);
                processor.Process(items.ToList(), context, settings);
            }
        }

        private static void WriteTemplate(string cwd, params Tuple<string, string>[] files)
        {
            foreach (var file in files)
            {
                var filePath = Path.Combine(cwd ?? string.Empty, file.Item1);
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(filePath, file.Item2);
            }
        }

        private static void WriteModel(string path, object model)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            JsonUtility.Serialize(path, model);
        }
    }
}
