namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    internal class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }
        public object Value { get; private set; }

        private ValidationResult(bool isValid, string errorMessage = null, object value = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            Value = value;
        }

        public static ValidationResult Valid(object value = null) => new(true, null, value);
        public static ValidationResult Invalid(string errorMessage) => new(false, errorMessage);

        public T GetValue<T>() => Value is T value ? value : default;
    }
}