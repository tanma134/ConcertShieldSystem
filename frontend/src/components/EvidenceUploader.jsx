import React, { useState } from "react";
import "./EvidenceUploader.css";

export default function EvidenceUploader({
  value = "",
  onChange,
  maxSizeMb = 8,
  label = "Supporting Document / Evidence",
  optional = true,
  hint = "Attaching medical notes, travel disruption proofs, or official notices helps expedite the review."
}) {
  const [fileName, setFileName] = useState("");
  const [fileSize, setFileSize] = useState("");
  const [preview, setPreview] = useState(() => {
    return value && value.startsWith("data:image") ? value : "";
  });
  const [isPdf, setIsPdf] = useState(() => {
    return Boolean(value && value.startsWith("data:application/pdf"));
  });

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > maxSizeMb * 1024 * 1024) {
      alert(`File size must not exceed ${maxSizeMb} MB.`);
      return;
    }

    setFileName(file.name);
    setFileSize(`${(file.size / 1024).toFixed(1)} KB`);

    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result;
      if (typeof onChange === "function") {
        onChange(dataUrl);
      }
      if (file.type.startsWith("image/")) {
        setPreview(dataUrl);
        setIsPdf(false);
      } else {
        setPreview("");
        setIsPdf(true);
      }
    };
    reader.readAsDataURL(file);
  };

  const handleRemove = () => {
    setFileName("");
    setFileSize("");
    setPreview("");
    setIsPdf(false);
    if (typeof onChange === "function") {
      onChange("");
    }
  };

  return (
    <div className="evidence-uploader-wrap">
      <div className="evidence-uploader-header">
        <label className="evidence-uploader-label">
          <span>{label}</span>
          {optional && <span className="evidence-uploader-optional">(Optional)</span>}
        </label>
      </div>

      <div className="evidence-uploader-dropzone">
        {value && (fileName || preview || isPdf) ? (
          <div className="evidence-uploader-preview-wrap">
            {preview ? (
              <img src={preview} alt="Evidence preview" className="evidence-uploader-thumb" />
            ) : (
              <span className="evidence-uploader-doc-badge">DOCUMENT</span>
            )}
            <div className="evidence-uploader-file-info">
              <span className="evidence-uploader-file-name">{fileName || "Attached file"}</span>
              {fileSize && <span className="evidence-uploader-file-size">{fileSize}</span>}
            </div>
            <button
              type="button"
              className="evidence-uploader-remove-btn"
              onClick={handleRemove}
            >
              Remove
            </button>
          </div>
        ) : (
          <label className="evidence-uploader-choose-label">
            <input
              type="file"
              accept="image/*,.pdf"
              onChange={handleFileChange}
              style={{ display: "none" }}
            />
            <span className="evidence-uploader-choose-text">Click to choose a file from your device</span>
            <small className="evidence-uploader-choose-sub">
              Supported formats: JPG, PNG, PDF (Up to {maxSizeMb} MB)
            </small>
          </label>
        )}
      </div>

      {hint && <small className="evidence-uploader-hint">{hint}</small>}
    </div>
  );
}
