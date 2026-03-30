import { createRoot } from "react-dom/client";
import "../ui/styles.css";
import "./settings.css";

const SettingsPage = (): JSX.Element => {
  return <main className="app-shell settings-page" aria-label="Foqus Settings" />;
};

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root container not found.");
}

createRoot(container).render(<SettingsPage />);
