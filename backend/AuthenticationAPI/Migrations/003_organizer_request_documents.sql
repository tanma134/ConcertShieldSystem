-- Add tax_code and representative_name to organizer_requests
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS representative_name VARCHAR(200);

-- Create organizer_request_documents table if not exists
CREATE TABLE IF NOT EXISTS organizer_request_documents (
    document_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    request_id INT NOT NULL REFERENCES organizer_requests(request_id) ON DELETE CASCADE,
    document_type VARCHAR(50) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_url TEXT NOT NULL,
    preview_url TEXT,
    public_id VARCHAR(255),
    is_image BOOLEAN NOT NULL DEFAULT TRUE,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Ensure public_id column exists if table was created previously without it
ALTER TABLE organizer_request_documents ADD COLUMN IF NOT EXISTS public_id VARCHAR(255);

CREATE INDEX IF NOT EXISTS idx_organizer_request_documents_request_id ON organizer_request_documents(request_id);
