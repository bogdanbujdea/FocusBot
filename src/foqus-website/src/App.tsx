import { Link, Routes, Route, useLocation } from "react-router-dom";
import { LandingPage } from "./pages/LandingPage";
import { TermsPage } from "./pages/TermsPage";
import { PrivacyPage } from "./pages/PrivacyPage";
import { RefundPage } from "./pages/RefundPage";
import "./App.css";

function App() {
  const location = useLocation();
  const isLegalPage = ["/terms", "/privacy", "/refund"].includes(location.pathname);

  return (
    <div className="app-root">
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/refund" element={<RefundPage />} />
      </Routes>

      {!isLegalPage && (
        <footer className="landing-footer">
          <div className="landing-footer-inner">
            <span className="landing-footer-brand">Foqus</span>
            <nav className="footer-legal-links" aria-label="Legal">
              <Link to="/terms">Terms</Link>
              <Link to="/privacy">Privacy</Link>
              <Link to="/refund">Refunds</Link>
            </nav>
            <span className="muted">© {new Date().getFullYear()}</span>
          </div>
        </footer>
      )}
    </div>
  );
}

export default App;
