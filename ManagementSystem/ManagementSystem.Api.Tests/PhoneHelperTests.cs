using ManagementSystem.Api.Common;

namespace ManagementSystem.Api.Tests
{
    public class PhoneHelperTests
    {
        [Theory]
        [InlineData("0912345678", "0912345678")]
        [InlineData("+84912345678", "0912345678")]
        [InlineData("84912345678", "0912345678")]
        [InlineData("+84 912 345 678", "0912345678")]
        [InlineData("091.234.5678", "0912345678")]
        [InlineData("(091) 234-5678", "0912345678")]
        public void NormalizePhone_ValidInputs_ReturnsCanonical10Digits(string input, string expected)
        {
            Assert.True(PhoneHelper.TryNormalizePhone(input, out var canonical));
            Assert.Equal(expected, canonical);
            Assert.Equal(expected, PhoneHelper.NormalizePhone(input));
            Assert.True(PhoneHelper.IsValidPhone(input));
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("091234567899")]
        [InlineData("phone-number")]
        [InlineData("+84+912345678")]
        public void NormalizePhone_InvalidInputs_ReturnsFalseAndEmpty(string input)
        {
            Assert.False(PhoneHelper.TryNormalizePhone(input, out var canonical));
            Assert.Empty(canonical);
            Assert.False(PhoneHelper.IsValidPhone(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizePhone_NullOrWhitespace_ReturnsEmptyString(string? input)
        {
            Assert.True(PhoneHelper.TryNormalizePhone(input, out var canonical));
            Assert.Empty(canonical);
            Assert.False(PhoneHelper.IsValidPhone(input));
        }
    }
}
