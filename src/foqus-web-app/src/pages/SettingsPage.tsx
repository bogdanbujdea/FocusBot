import { useState } from "react";
import { useAuth } from "../auth/useAuth";
import { api } from "../api/client";
import "./SettingsPage.css";

export function SettingsPage() {
  const { user, signOut } = useAuth();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [confirmText, setConfirmText] = useState("");

  const handleDeleteAccount = async () => {
    if (confirmText !== "DELETE") return;
    setDeleting(true);
    const result = await api.deleteAccount();
    if (result) {
      await signOut();
    } else {
      setDeleting(false);
      alert("Failed to delete account. Please try again.");
    }
  };

  return (
    <div className="settings-page">
      <header className="page-header">
        <h1 className="page-title">Settings</h1>
        <p className="page-subtitle">Manage your account and preferences</p>
      </header>

      <section className="settings-section">
        <h2 className="section-title">Account</h2>
        <div className="settings-card">
          <div className="setting-row">
            <div className="setting-info">
              <div className="setting-label">Email</div>
              <div className="setting-value">{user?.email}</div>
            </div>
          </div>
          <div className="setting-row">
            <div className="setting-info">
              <div className="setting-label">Sign out</div>
              <div className="setting-description">
                Sign out of your Foqus account on this browser
              </div>
            </div>
            <button onClick={signOut} className="settings-btn">
              Sign out
            </button>
          </div>
        </div>
      </section>

      <section className="settings-section danger-zone">
        <h2 className="section-title">Danger Zone</h2>
        <div className="settings-card danger-card">
          <div className="setting-row">
            <div className="setting-info">
              <div className="setting-label">Delete account</div>
              <div className="setting-description">
                Permanently delete your account and all associated data. This
                action cannot be undone.
              </div>
            </div>
            {!showDeleteConfirm ? (
              <button
                onClick={() => setShowDeleteConfirm(true)}
                className="settings-btn danger-btn"
              >
                Delete account
              </button>
            ) : (
              <div className="delete-confirm">
                <p className="delete-warning">
                  Type <strong>DELETE</strong> to confirm:
                </p>
                <input
                  type="text"
                  value={confirmText}
                  onChange={(e) => setConfirmText(e.target.value)}
                  className="form-input delete-input"
                  placeholder="Type DELETE"
                  disabled={deleting}
                />
                <div className="delete-actions">
                  <button
                    onClick={handleDeleteAccount}
                    disabled={confirmText !== "DELETE" || deleting}
                    className="settings-btn danger-btn"
                  >
                    {deleting ? "Deleting..." : "Confirm delete"}
                  </button>
                  <button
                    onClick={() => {
                      setShowDeleteConfirm(false);
                      setConfirmText("");
                    }}
                    className="settings-btn"
                    disabled={deleting}
                  >
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}
