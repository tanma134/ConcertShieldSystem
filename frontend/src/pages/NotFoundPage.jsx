import { Link } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import "../styles/theme-dark.css";
import "./NotFoundPage.css";

export default function NotFoundPage() {
  return (
    <div className="tb-app not-found-page">
      <Header />
      <main className="not-found-main">
        <section className="not-found-card" aria-labelledby="not-found-title">
          <div className="not-found-code" aria-hidden="true">404</div>
          <span className="not-found-eyebrow">PAGE NOT FOUND</span>
          <h1 id="not-found-title">We can&apos;t find that page</h1>
          <p>
            The link may be incorrect, or the page may have been moved. Check the
            address and try again, or return to the home page.
          </p>
          <Link to="/" className="tb-btn tb-btn-primary not-found-home">
            Back to Home
          </Link>
        </section>
      </main>
      <Footer />
    </div>
  );
}
