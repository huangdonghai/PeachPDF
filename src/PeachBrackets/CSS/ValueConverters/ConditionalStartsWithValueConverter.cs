#nullable disable

using System.Collections.Generic;
using System.Linq;

namespace PeachBrackets.Css
{
    internal sealed class ConditionalStartsWithValueConverter : IValueConverter
    {
        private readonly string _when;
        private readonly string[] _prefixKeywords;
        private readonly IValueConverter _converter;

        public ConditionalStartsWithValueConverter(string when, IValueConverter converter, params string[] prefixKeywords)
        {
            _when = when;
            _prefixKeywords = prefixKeywords;
            _converter = converter;
        }

        public IPropertyValue Convert(IReadOnlyList<Token> value)
        {
            var enumerator = value.GetEnumerator();

            var found = false;

            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Type == TokenType.Whitespace)
                    continue;

                found = true;
                break;
            }

            // A half of a two-value shorthand (place-self's justify side, an empty declaration) has no
            // token to read: Current after the final MoveNext is undefined, and an array-backed list
            // throws for it rather than answering a default.
            if (!found || enumerator.Current.Type != TokenType.Ident)
                return null;

            var consumedPrefix = _prefixKeywords.FindIs(enumerator.Current.Data);
            if (consumedPrefix is null)
                return null;

            var remainingTokens = new List<Token>();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Type == TokenType.Whitespace)
                    continue;

                remainingTokens.Add(enumerator.Current);
            }

            var result = _converter.Convert(remainingTokens);

            // The prefix has to introduce a value the wrapped converter accepts: "safe nonsense" is an
            // invalid declaration. Letting a null result through built a value with no CssText to read,
            // which threw the moment anything asked for the property's text.
            if (result is null || result.CssText != _when)
                return null;

            return new ConditionalStartValue(consumedPrefix, result, value);
        }

        public IPropertyValue Construct(Property[] properties)
        {
            var value = _converter.Construct(properties);
            return value != null ? CreateFrom(value, Enumerable.Empty<Token>()) : null;
        }

        private IPropertyValue CreateFrom(IPropertyValue value, IEnumerable<Token> tokens)
        {
            return value != null ? new ConditionalStartValue(string.Empty, value, tokens) : null;
        }

        private sealed class ConditionalStartValue : IPropertyValue
        {
            private readonly string _start;
            private readonly IPropertyValue _value;

            public ConditionalStartValue(string start, IPropertyValue value, IEnumerable<Token> tokens)
            {
                _start = start;
                _value = value;
                Original = new TokenValue(tokens);
            }

            public string CssText
            {
                get
                {
                    return !string.IsNullOrEmpty(_start)
                               ? string.Concat(_start, " ", _value.CssText)
                               : _value.CssText;
                }
            }

            public TokenValue Original { get; }

            public TokenValue ExtractFor(string name)
            {
                return _value.ExtractFor(name);
            }
        }
    }
}
