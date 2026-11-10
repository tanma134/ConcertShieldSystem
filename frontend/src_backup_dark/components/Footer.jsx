import { Link } from "react-router-dom";
import "./Footer.css";

export default function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="tb-footer">
      <div className="tb-container tb-footer-grid">
        <div className="tb-footer-brand">
          <Link to="/" className="tb-footer-logo">
            Concert<span>Shield</span>
          </Link>
          <p>
            Vietnam's leading concert ticketing platform. Top-tier music
            experiences - easy booking - instant tickets.
          </p>
          <div className="tb-footer-contact">
            <span>support@concertshield.vn</span>
            <span>Hotline: 1900 1234</span>
          </div>
        </div>

        <div className="tb-footer-col">
          <h4>Support</h4>
          <a href="#faq">FAQ</a>
          <a href="#contact">Contact Us</a>
          <a href="#refund">Refund Policy</a>
          <a href="#terms">Terms of Service</a>
        </div>

        <div className="tb-footer-col">
          <h4>Explore</h4>
          <Link to="/">All Concerts</Link>
          <Link to="/">Hot Concerts</Link>
          <Link to="/">Upcoming</Link>
        </div>

        <div className="tb-footer-col">
          <h4>Follow Us</h4>
          <a href="https://facebook.com" target="_blank" rel="noreferrer">
            Facebook
          </a>
          <a href="https://instagram.com" target="_blank" rel="noreferrer">
            Instagram
          </a>
          <a href="https://tiktok.com" target="_blank" rel="noreferrer">
            TikTok
          </a>
        </div>
      </div>

      <div className="tb-footer-bottom tb-container">
        © {year} ConcertShield. All rights reserved.
      </div>
    </footer>
  );
}
