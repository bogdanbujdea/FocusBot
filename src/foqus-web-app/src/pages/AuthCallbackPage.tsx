import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { supabase } from "../auth/supabase";

export function AuthCallbackPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handleCallback = async () => {
      const code = searchParams.get("code");

      if (code) {
        const { error } = await supabase.auth.exchangeCodeForSession(code);
        if (error) {
          setError(error.message);
          return;
        }
        navigate("/", { replace: true });
        return;
      }

      const hashParams = new URLSearchParams(
        window.location.hash.substring(1)
      );
      if (hashParams.get("access_token")) {
        const { error } = await supabase.auth.getSession();
        if (error) {
          setError(error.message);
          return;
        }
        navigate("/", { replace: true });
        return;
      }

      const errorDescription =
        searchParams.get("error_description") ??
        hashParams.get("error_description");
      if (errorDescription) {
        setError(errorDescription);
        return;
      }

      setError(
        "No authentication code found. The link may have expired — please request a new one."
      );
    };

    handleCallback();
  }, [navigate, searchParams]);

  if (error) {
    return (
      <div className="loading-container">
        <div style={{ textAlign: "center" }}>
          <p style={{ color: "var(--color-distracted)", marginBottom: "16px" }}>
            {error}
          </p>
          <a href="/login" style={{ color: "var(--color-primary)" }}>
            Return to login
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="loading-container">
      <div className="loading-spinner" />
    </div>
  );
}
