import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import eventApi from "../api/eventApi";
import "./Header.css";



// Quick-pick keywords shown under the search box so users can tap instead
// of typing (and don't need to clear the box first - picking one just
// replaces whatever is currently in it).
const POPULAR_SEARCHES = ["Live Music", "EDM Festival", "K-Pop", "Jazz Night", "Acoustic"];

export default function Header() {
  const navigate = useNavigate();
  const { user, logout, isAuthenticated, isAdmin } = useAuth();
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState(searchParams.get("q") || "");
  const [menuOpen, setMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [suggestLoading, setSuggestLoading] = useState(false);
  const accountRef = useRef(null);
  const searchRef = useRef(null);
  const debounceRef = useRef(null);

  const runSearch = (term) => {
    const trimmed = term.trim();
    setSearchOpen(false);
    navigate(trimmed ? `/?q=${encodeURIComponent(trimmed)}` : "/");
  };

  const handleSearch = (e) => {
    e.preventDefault();
    runSearch(keyword);
  };

  const handlePickSuggestion = (term) => {
    setKeyword(term);
    runSearch(term);
  };

  const handleLogout = () => {
    logout();
    setMenuOpen(false);
    navigate("/");
  };

  // Live suggestions as the user types (debounced).
  useEffect(() => {
    const trimmed = keyword.trim();
    if (debounceRef.current) clearTimeout(debounceRef.current);

    if (!trimmed) {
      setSuggestions([]);
      setSuggestLoading(false);
      return;
    }

    setSuggestLoading(true);
    debounceRef.current = setTimeout(() => {
      eventApi
        .getList({ search: trimmed, page: 1, pageSize: 5 })
        .then((res) => setSuggestions(res.data?.data?.items || []))
        .catch(() => setSuggestions([]))
        .finally(() => setSuggestLoading(false));
    }, 300);

    return () => clearTimeout(debounceRef.current);
  }, [keyword]);

  // Close the dropdown on outside click or Esc, instead of onMouseLeave
  // (there used to be a gap between the button and the menu that closed
  // the menu on mouseover, making the "Log Out" button unclickable).
  useEffect(() => {
    if (!menuOpen && !searchOpen) return;

    const handleClickOutside = (e) => {
      if (accountRef.current && !accountRef.current.contains(e.target)) {
        setMenuOpen(false);
      }
      if (searchRef.current && !searchRef.current.contains(e.target)) {
        setSearchOpen(false);
      }
    };
    const handleEsc = (e) => {
      if (e.key === "Escape") {
        setMenuOpen(false);
        setSearchOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEsc);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEsc);
    };
  }, [menuOpen, searchOpen]);

  return (
    <header className="tb-header">
      <div className="tb-header-top tb-container">
        <Link to="/" className="tb-logo">
          <span className="tb-logo-mark">
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <path
                d="M12 2 3 6v6c0 5 3.8 8.7 9 10 5.2-1.3 9-5 9-10V6l-9-4Z"
                fill="url(#tb-shield-grad)"
              />
              <path
                d="M8.5 12.3 11 14.8l4.7-5.4"
                stroke="#fff"
                strokeWidth="1.6"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <defs>
                <linearGradient id="tb-shield-grad" x1="3" y1="2" x2="21" y2="22">
                  <stop offset="0" stopColor="#7c3aed" />
                  <stop offset="0.55" stopColor="#c026d3" />
                  <stop offset="1" stopColor="#ec4899" />
                </linearGradient>
              </defs>
            </svg>
          </span>
          Concert<span>Shield</span>
        </Link>

        <div className="tb-search-wrap" ref={searchRef}>
          <form className="tb-search" onSubmit={handleSearch}>
            <svg
              className="tb-search-icon"
              viewBox="0 0 20 20"
              width="16"
              height="16"
              fill="none"
            >
              <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth="1.6" />
              <path
                d="m18 18-4-4"
                stroke="currentColor"
                strokeWidth="1.6"
                strokeLinecap="round"
              />
            </svg>
            <input
              type="text"
              placeholder="Search concerts, artists..."
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onFocus={() => setSearchOpen(true)}
            />
            <button type="submit">Search</button>
          </form>

          {searchOpen && (
            <div className="tb-search-dropdown">
              {keyword.trim() && (
                <div className="tb-search-section">
                  <span className="tb-search-section-label">Suggestions</span>
                  {suggestLoading && (
                    <div className="tb-search-hint">Searching...</div>
                  )}
                  {!suggestLoading && suggestions.length === 0 && (
                    <div className="tb-search-hint">No matching events</div>
                  )}
                  {!suggestLoading &&
                    suggestions.map((ev) => (
                      <Link
                        key={ev.eventId}
                        to={`/events/${ev.slug}`}
                        className="tb-search-suggestion"
                        onClick={() => setSearchOpen(false)}
                      >
                        {ev.title}
                      </Link>
                    ))}
                </div>
              )}

              <div className="tb-search-section">
                <span className="tb-search-section-label">Popular searches</span>
                <div className="tb-search-chips">
                  {POPULAR_SEARCHES.map((term) => (
                    <button
                      type="button"
                      key={term}
                      className="tb-search-chip"
                      onClick={() => handlePickSuggestion(term)}
                    >
                      {term}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="tb-header-actions">
          {isAuthenticated && (
            <button
              type="button"
              className="tb-btn tb-btn-outline tb-create-btn"
              onClick={() => navigate("/organizer/events/new")}
            >
              <svg viewBox="0 0 20 20" width="15" height="15" fill="none">
                <path
                  d="M10 4v12M4 10h12"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                />
              </svg>
              <span>Create Event</span>
            </button>
          )}

          {isAuthenticated && (
            <Link to="/organizer/events" className="tb-header-link">
              My Concerts
            </Link>
          )}

          {isAuthenticated && (
            <Link to="/my-tickets" className="tb-header-link">
              My Tickets
            </Link>
          )}

          {isAdmin && (
            <Link to="/admin/events" className="tb-header-link">
              Admin
            </Link>
          )}

          <div className="tb-account" ref={accountRef}>
            {isAuthenticated ? (
              <>
                <button
                  type="button"
                  className="tb-header-link tb-account-toggle"
                  onClick={() => setMenuOpen((v) => !v)}
                >
                  {user?.fullName || user?.email || "Account"}
                  <svg
                    viewBox="0 0 20 20"
                    width="12"
                    height="12"
                    fill="none"
                    className={"tb-account-caret" + (menuOpen ? " open" : "")}
                  >
                    <path
                      d="m5 7 5 6 5-6"
                      stroke="currentColor"
                      strokeWidth="1.6"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
                {menuOpen && (
                  <div className="tb-account-menu">
                    <Link to="/profile" onClick={() => setMenuOpen(false)}>
                      My Profile
                    </Link>
                    <button type="button" onClick={handleLogout}>
                      Log Out
                    </button>
                  </div>
                )}
              </>
            ) : (
              <Link to="/login" className="tb-btn tb-btn-primary tb-login-btn">
                Log In
              </Link>
            )}
          </div>
        </div>
      </div>

    </header>
  );
}
