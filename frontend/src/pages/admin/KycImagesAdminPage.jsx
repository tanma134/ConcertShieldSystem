import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import kycAdminApi from "../../api/kycAdminApi";
import AdminShell from "./AdminShell";
import "./kycAdmin.css";

const KINDS = [
  ["front", "ID card - front"],
  ["back", "ID card - back"],
  ["selfie", "Selfie"],
];

export default function KycImagesAdminPage() {
  const [params, setParams] = useSearchParams();
  const ekycId = params.get("ekycId") || "";
  const [input, setInput] = useState(ekycId);
  const [images, setImages] = useState({}); // kind -> {loading, url, error}
  const urlsRef = useRef({});

  const revokeAll = () => {
    Object.values(urlsRef.current).forEach((u) => URL.revokeObjectURL(u));
    urlsRef.current = {};
    setImages({});
  };

  // Drop the images when the eKYC id changes or the page unmounts
  useEffect(() => { revokeAll(); setInput(ekycId); return revokeAll; }, [ekycId]);

  const search = (e) => {
    e.preventDefault();
    const id = input.trim();
    if (/^\d+$/.test(id)) setParams({ ekycId: id });
  };

  const reveal = async (kind) => {
    setImages((s) => ({ ...s, [kind]: { loading: true } }));
    try {
      const res = await kycAdminApi.getImage(ekycId, kind);
      const url = URL.createObjectURL(res.data);
      if (urlsRef.current[kind]) URL.revokeObjectURL(urlsRef.current[kind]);
      urlsRef.current[kind] = url;
      setImages((s) => ({ ...s, [kind]: { url } }));
    } catch (e) {
      const st = e.response?.status;
      const error =
        st === 404 ? "Image not found (it may have been deleted)."
        : st === 403 ? "You don't have permission to view this image."
        : st === 500 ? "The view couldn't be logged, so the image was not shown."
        : "Failed to load the image.";
      setImages((s) => ({ ...s, [kind]: { error } }));
    }
  };

  const hide = (kind) => {
    if (urlsRef.current[kind]) URL.revokeObjectURL(urlsRef.current[kind]);
    delete urlsRef.current[kind];
    setImages((s) => ({ ...s, [kind]: undefined }));
  };

  return (
    <AdminShell title="eKYC Images">
      <section className="admin-panel">
        <div className="panel-header"><h2>Original eKYC images</h2></div>
        <div className="kyc-admin-warn">
          Sensitive personal data. Every image you open is recorded in the access log with your admin ID and IP address.
        </div>

        <form className="kyc-admin-toolbar" onSubmit={search}>
          <input inputMode="numeric" value={input} onChange={(e) => setInput(e.target.value)} placeholder="eKYC ID (e.g. 7)" />
          <button type="submit" className="admin-button">Load</button>
        </form>

        {!ekycId ? (
          <div className="empty-state">Enter an eKYC ID to see its images.</div>
        ) : (
          <div className="kyc-images">
            {KINDS.map(([kind, label]) => {
              const s = images[kind];
              return (
                <div className="kyc-image-card" key={kind}>
                  <div className="kyc-image-card__title">{label}</div>
                  <div className="kyc-image-card__body">
                    {s?.url ? (
                      <img src={s.url} alt={label} />
                    ) : s?.loading ? (
                      "Loading..."
                    ) : (
                      <div>
                        {s?.error && <p className="kyc-image-card__err">{s.error}</p>}
                        <button type="button" className="admin-button-secondary" onClick={() => reveal(kind)}>
                          {s?.error ? "Try again" : "View image"}
                        </button>
                      </div>
                    )}
                  </div>
                  {s?.url && (
                    <div style={{ padding: 10, textAlign: "right" }}>
                      <button type="button" className="text-button" onClick={() => hide(kind)}>Hide</button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </section>
    </AdminShell>
  );
}
