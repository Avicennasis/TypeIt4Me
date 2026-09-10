using System.Threading.Tasks;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class InputInjectorTests
    {
        [Fact]
        public async Task TypeTextAsync_Plain_SendsBatch()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("Hello");

            Assert.Contains("RELEASE_MODIFIERS", mock.Log);
            Assert.Contains("SEND_BATCH:Hello", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_SpecialKey_SendsVirtualKey()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("{TAB}");

            Assert.Contains("SEND_VK:0x09", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_SleepCommand_AddsDelay()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("{SLEEP 500}");

            Assert.Contains("DELAY:500", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_UnknownCommand_TypesLiterally()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("{UNKNOWN}");

            // Should be sent as plain text including braces
            Assert.Contains("SEND_BATCH:{UNKNOWN}", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_EmptyBraces_TypesLiterally()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("{}");

            Assert.Contains("SEND_BATCH:{}", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_EmptyBraces_WithinText_TypesLiterally()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("A{}B");

            Assert.Contains("SEND_BATCH:A{}B", mock.Log);
        }

        [Fact]
        public async Task TypeTextAsync_MixedContent_HandlesSequence()
        {
            var mock = new MockInputSender();
            var injector = new InputInjector(mock);

            await injector.TypeTextAsync("User{TAB}Pass{ENTER}");

            Assert.Equal("RELEASE_MODIFIERS", mock.Log[0]);
            Assert.Equal("DELAY:150", mock.Log[1]);
            Assert.Equal("SEND_BATCH:User", mock.Log[2]);
            Assert.Equal("DELAY:10", mock.Log[3]);
            Assert.Equal("SEND_VK:0x09", mock.Log[4]);
            Assert.Equal("DELAY:10", mock.Log[5]);
            Assert.Equal("SEND_BATCH:Pass", mock.Log[6]);
            Assert.Equal("DELAY:10", mock.Log[7]);
            Assert.Equal("SEND_VK:0x0D", mock.Log[8]);
            Assert.Equal("DELAY:10", mock.Log[9]);
        }
    }
}
