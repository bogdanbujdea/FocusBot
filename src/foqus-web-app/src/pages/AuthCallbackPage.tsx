import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { supabase } from "../auth/supabase";

export function AuthCallbackPage() {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handleCallback = async () => {
      const { error } = await supabase.auth.getSession();
      if (error) {
        setError(error.message);
      } else {
        navigate("/", { replace: true });
      }
    };

    handleCallback();
  }, [navigate]);

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
