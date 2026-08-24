import { forwardRef, useState, type InputHTMLAttributes } from "react";

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
}

export const FormField = forwardRef<HTMLInputElement, FormFieldProps>(
  ({ label, error, id, ...inputProps }, ref) => {
    const inputId = id ?? inputProps.name;
    const errorId = error ? `${inputId}-error` : undefined;
    const isPassword = inputProps.type === "password";
    const [passwordVisible, setPasswordVisible] = useState(false);

    return (
      <div className="form-field">
        <label htmlFor={inputId}>{label}</label>
        <div className={isPassword ? "field-control password-control" : "field-control"}>
          <input
            {...inputProps}
            type={isPassword && passwordVisible ? "text" : inputProps.type}
            ref={ref}
            id={inputId}
            aria-invalid={Boolean(error)}
            aria-describedby={errorId}
          />
          {isPassword && (
            <button
              type="button"
              className="password-toggle"
              aria-label={passwordVisible ? "Hide password" : "Show password"}
              aria-pressed={passwordVisible}
              onClick={() => setPasswordVisible((visible) => !visible)}
            >
              {passwordVisible ? "Hide" : "Show"}
            </button>
          )}
        </div>
        {error && <span className="field-error" id={errorId} role="alert">{error}</span>}
      </div>
    );
  },
);

FormField.displayName = "FormField";
