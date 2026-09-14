using System;

namespace CursorHunter.Contracts
{
    /// <summary>
    /// Stable identity for one logical run. A RunId is never reused for a new
    /// combat attempt; settlement retries reuse the original value instead.
    /// </summary>
    public readonly struct RunId : IEquatable<RunId>
    {
        private readonly string _value;

        public RunId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("RunId must not be empty.", nameof(value));
            }

            _value = value.Trim();
        }

        public string Value => _value ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(_value);

        public static RunId Create()
        {
            return new RunId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(RunId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RunId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(RunId left, RunId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RunId left, RunId right)
        {
            return !left.Equals(right);
        }
    }
}
