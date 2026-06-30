import { useState, type FormEvent } from "react";
import { useAuth } from "../../auth/AuthContext";
import { useI18n } from "../../i18n";
import { Emblem } from "../../components/Emblem";

/** Login screen (FR-01). RTL-first; submits to /api/auth/login via the auth context. */
export function LoginPage({ onSuccess }: { onSuccess: () => void }) {
  const { login } = useAuth();
  const { lang } = useI18n();
  const ar = lang === "ar";

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login(username.trim(), password);
      onSuccess();
    } catch (err) {
      setError(ar ? "بيانات الدخول غير صحيحة" : "Invalid credentials");
      void err;
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login">
      <form className="login__card" onSubmit={submit}>
        <Emblem size={64} />
        <h1 className="login__title">{ar ? "تسجيل الدخول" : "Sign in"}</h1>
        <p className="login__sub">{ar ? "نظام ديوان النسخ القضائي" : "Judicial Copying System"}</p>

        <label className="login__field">
          <span>{ar ? "اسم المستخدم" : "Username"}</span>
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" required />
        </label>

        <label className="login__field">
          <span>{ar ? "كلمة المرور" : "Password"}</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>

        {error && <div className="login__error" role="alert">{error}</div>}

        <button className="login__submit" type="submit" disabled={busy}>
          {busy ? (ar ? "جارٍ الدخول…" : "Signing in…") : (ar ? "دخول" : "Sign in")}
        </button>
      </form>
    </div>
  );
}
