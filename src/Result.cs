using System.Diagnostics.CodeAnalysis;

namespace First {
    public readonly record struct Result {
        public static Result Success() => default;
        public static Success<TValue> Success<TValue>(TValue value) => new(value);
        public static Failure<TError> Failure<TError>(TError error) => new(error);
    }

    public readonly record struct Success<TValue> {
        public TValue Value { get; }

        internal Success(TValue value) {
            Value = value;
        }
    }

    public readonly record struct Failure<TError> {
        public TError Error { get; }

        internal Failure(TError error) {
            Error = error;
        }
    }

    public readonly record struct Result<TValue, TError> {
        private readonly TValue? _value;
        private readonly TError? _error;
        private readonly bool _isSuccess;
        private readonly bool _isInitialized;

        public bool IsSuccess => _isInitialized
            ? _isSuccess
            : throw new InvalidOperationException("Cannot access IsSuccess on an uninitialized Result.");

        public bool IsFailure => _isInitialized
            ? !_isSuccess
            : throw new InvalidOperationException("Cannot access IsFailure on an uninitialized Result.");

        public TValue Value => !_isInitialized
            ? throw new InvalidOperationException("Cannot access Value on an uninitialized Result.")
            : _isSuccess
                ? _value!
                : throw new InvalidOperationException("Cannot access Value on a failed Result.");

        public TError Error => !_isInitialized
            ? throw new InvalidOperationException("Cannot access Error on an uninitialized Result.")
            : !_isSuccess
                ? _error!
                : throw new InvalidOperationException("Cannot access Error on a successful Result.");

        private Result(bool isSuccess, TValue? value, TError? error) {
            _isInitialized = true;
            _isSuccess = isSuccess;
            _value = value;
            _error = error;
        }

        public static Result<TValue, TError> Success(TValue value) => new(true, value, default);
        public static Result<TValue, TError> Failure(TError error) => new(false, default, error);

        public static implicit operator Result<TValue, TError>(TValue value) => Success(value);
        public static implicit operator Result<TValue, TError>(TError error) => Failure(error);
        public static implicit operator Result<TValue, TError>(Success<TValue> success) => Result<TValue, TError>.Success(success.Value);
        public static implicit operator Result<TValue, TError>(Failure<TError> failure) => Result<TValue, TError>.Failure(failure.Error);

        public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure) {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);
            if (!_isInitialized) throw new InvalidOperationException("Cannot match an uninitialized Result.");
            return _isSuccess ? onSuccess(_value!) : onFailure(_error!);
        }

        public bool Verify([MaybeNullWhen(false)] out TValue value, [MaybeNullWhen(true)] out TError error) {
            if (!_isInitialized) throw new InvalidOperationException("Cannot verify an uninitialized Result.");
            value = _value;
            error = _error;
            return _isSuccess;
        }

        public override string ToString() => !_isInitialized
            ? "Uninitialized"
            : _isSuccess
                ? $"Success({_value})"
                : $"Failure({_error})";
    }

    public readonly record struct Failable<TError> {
        private readonly TError? _error;
        private readonly bool _isFailure;
        private readonly bool _isInitialized;

        public bool IsFailure => _isInitialized
            ? _isFailure
            : throw new InvalidOperationException("Cannot access IsFailure on an uninitialized Failable.");

        public bool IsSuccess => _isInitialized
            ? !_isFailure
            : throw new InvalidOperationException("Cannot access IsSuccess on an uninitialized Failable.");

        public TError Error => !_isInitialized
            ? throw new InvalidOperationException("Cannot access Error on an uninitialized Failable.")
            : _isFailure
                ? _error!
                : throw new InvalidOperationException("Cannot access Error on a successful Failable.");

        private Failable(bool isFailure, TError? error) {
            _isInitialized = true;
            _isFailure = isFailure;
            _error = error;
        }

        public static Failable<TError> Success() => new(false, default);
        public static Failable<TError> Failure(TError error) => new(true, error);

        public static implicit operator Failable<TError>(Result _) => Success();
        public static implicit operator Failable<TError>(TError error) => Failure(error);
        public static implicit operator Failable<TError>(Failure<TError> failure) => Failable<TError>.Failure(failure.Error);

        public TResult Match<TResult>(Func<TResult> onSuccess, Func<TError, TResult> onFailure) {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);
            if (!_isInitialized) throw new InvalidOperationException("Cannot match an uninitialized Failable.");
            return _isFailure ? onFailure(_error!) : onSuccess();
        }

        public bool Verify([MaybeNullWhen(true)] out TError error) {
            if (!_isInitialized) throw new InvalidOperationException("Cannot verify an uninitialized Failable.");
            error = _error;
            return !_isFailure;
        }

        public override string ToString() => !_isInitialized
            ? "Uninitialized"
            : _isFailure
                ? $"Failure({_error})"
                : "Success";
    }
}
