/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using magic.node.extensions;
using magic.signals.contracts;
using magic.node;

namespace magic.lambda.io.tests
{
    public class IOTests
    {
        [Fact]
        public void SaveFile()
        {
            var saveInvoked = false;
            var existsInvoked = false;
            var fileService = new FileService
            {
                SaveAction = (path, content) =>
                {
                    Assert.Equal("foo", content);
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    saveInvoked = true;
                },
                ExistsAction = (path) =>
                {
                    existsInvoked = true;
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    return true;
                }
            };
            var lambda = Common.Evaluate(@"
io.files.save:existing.txt
   .:foo
io.files.exists:/existing.txt
", fileService);
            Assert.True(saveInvoked);
            Assert.True(existsInvoked);
            Assert.True(lambda.Children.Skip(1).First().Get<bool>());
        }

        [Fact]
        public void SaveAndLoadFile()
        {
            var saveInvoked = false;
            var loadInvoked = false;
            var fileService = new FileService
            {
                SaveAction = (path, content) =>
                {
                    Assert.Equal("foo", content);
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    saveInvoked = true;
                },
                LoadAction = (path) =>
                {
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    loadInvoked = true;
                    return "foo";
                }
            };
            var lambda = Common.Evaluate(@"
io.files.save:existing.txt
   .:foo
io.file.load:/existing.txt
", fileService);
            Assert.True(saveInvoked);
            Assert.True(loadInvoked);
            Assert.Equal("foo", lambda.Children.Skip(1).First().Get<string>());
        }

        [Fact]
        public async Task SaveAndLoadFileAsync()
        {
            var saveInvoked = false;
            var loadInvoked = false;
            var fileService = new FileService
            {
                SaveAsyncAction = async (path, content) =>
                {
                    Assert.Equal("foo", content);
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    saveInvoked = true;
                    await Task.Yield();
                },
                LoadAsyncAction = async (path) =>
                {
                    Assert.Equal(
                        AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "/").TrimEnd('/')
                        + "/" +
                        "existing.txt", path);
                    loadInvoked = true;
                    await Task.Yield();
                    return "foo";
                }
            };
            var lambda = await Common.EvaluateAsync(@"
wait.io.files.save:existing.txt
   .:foo
wait.io.file.load:/existing.txt
", fileService);
            Assert.True(saveInvoked);
            Assert.True(loadInvoked);
            Assert.Equal("foo", lambda.Children.Skip(1).First().Get<string>());
        }

        [Fact]
        public void SaveFileAndMove()
        {
            var lambda = Common.Evaluate(@"
if
   io.files.exists:/moved.txt
   .lambda
      io.files.delete:/moved.txt
io.files.save:/existing.txt
   .:foo
io.files.move:/existing.txt
   .:moved.txt
io.files.exists:/moved.txt
io.files.exists:/existing.txt
");
            Assert.True(lambda.Children.Skip(3).First().Get<bool>());
            Assert.False(lambda.Children.Skip(4).First().Get<bool>());
        }

        [Fact]
        public void SaveFileAndCopy()
        {
            var lambda = Common.Evaluate(@"
if
   io.files.exists:/moved-x.txt
   .lambda
      io.files.delete:/moved-x.txt
io.files.save:/existing-x.txt
   .:foo
io.files.copy:/existing-x.txt
   .:moved-x.txt
io.files.exists:/moved-x.txt
io.files.exists:/existing-x.txt
");
            Assert.True(lambda.Children.Skip(3).First().Get<bool>());
            Assert.True(lambda.Children.Skip(4).First().Get<bool>());
        }

        [Fact]
        public async Task SaveFileAndCopyAsync()
        {
            var lambda = await Common.EvaluateAsync(@"
if
   io.files.exists:/moved-x.txt
   .lambda
      io.files.delete:/moved-x.txt
io.files.save:/existing-x.txt
   .:foo
wait.io.files.copy:/existing-x.txt
   .:moved-x.txt
io.files.exists:/moved-x.txt
io.files.exists:/existing-x.txt
");
            Assert.True(lambda.Children.Skip(3).First().Get<bool>());
            Assert.True(lambda.Children.Skip(4).First().Get<bool>());
        }

        [Slot(Name = "foo")]
        public class EventSource : ISlot
        {
            public void Signal(ISignaler signaler, Node input)
            {
                input.Value = "success";
            }
        }

        [Fact]
        public void SaveWithEventSourceAndLoadFile()
        {
            var lambda = Common.Evaluate(@"
io.files.save:existing.txt
   foo:error
io.file.load:/existing.txt
");
            Assert.Equal("success", lambda.Children.Skip(1).First().Get<string>());
        }

        [Fact]
        public void SaveOverwriteAndLoadFile()
        {
            var lambda = Common.Evaluate(@"
io.files.save:existing.txt
   .:foo
io.files.save:existing.txt
   .:foo1
io.file.load:/existing.txt
");
            Assert.Equal("foo1", lambda.Children.Skip(2).First().Get<string>());
        }

        [Fact]
        public void EnsureFileDoesntExist()
        {
            var lambda = Common.Evaluate(@"
io.files.exists:/non-existing.txt
");
            Assert.False(lambda.Children.First().Get<bool>());
        }

        [Fact]
        public void ListFiles()
        {
            var lambda = Common.Evaluate(@"
io.files.list:/
");
            Assert.True(lambda.Children.First().Children.Count() > 5);
            Assert.True(lambda.Children.First().Children
                .Where(x => x.Get<string>().StartsWith("/")).Count() == lambda.Children.First().Children.Count());
        }

        [Fact]
        public void ListFolders()
        {
            var lambda = Common.Evaluate(@"
io.folder.list:/
");
        }

        [Fact]
        public void CreateFolderListFolders()
        {
            var lambda = Common.Evaluate(@"
io.folder.create:/foo
io.folder.list:/
");
            Assert.Single(lambda.Children.Skip(1).First().Children.Where(x => x.Get<string>().Contains("foo")));
            Assert.Equal("/foo/", lambda.Children.Skip(1).First().Children.Where(x => x.Get<string>().Contains("foo")).First().Get<string>());
        }

        [Fact]
        public void EvaluateFile()
        {
            var lambda = Common.Evaluate(@"
io.files.eval:foo-1.hl
");
            Assert.Single(lambda.Children.First().Children);
            Assert.Equal("hello world", lambda.Children.First().Children.First().Get<string>());
        }

        [Fact]
        public void EvaluateFileWithArguments()
        {
            var lambda = Common.Evaluate(@"
io.files.eval:foo-2.hl
   input:jo world
");
            Assert.Single(lambda.Children.First().Children);
            Assert.Equal("result", lambda.Children.First().Children.First().Name);
            Assert.Equal("jo world", lambda.Children.First().Children.First().Get<string>());
        }

        [Fact]
        public void EvaluateFileReturningValue()
        {
            var lambda = Common.Evaluate(@"io.files.eval:foo-3.hl");
            Assert.Empty(lambda.Children.First().Children);
            Assert.Equal("howdy world", lambda.Children.First().Get<string>());
        }
    }
}
