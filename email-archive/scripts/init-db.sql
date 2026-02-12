-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Emails table
CREATE TABLE IF NOT EXISTS emails (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id TEXT,
    thread_id TEXT,
    date TIMESTAMPTZ,
    sender TEXT,
    sender_name TEXT,
    recipients JSONB,
    subject TEXT,
    body_text TEXT,
    body_word_count INTEGER,
    is_sent BOOLEAN DEFAULT FALSE,
    has_attachments BOOLEAN DEFAULT FALSE,
    tier INTEGER,
    embedding vector(1536),
    embedded_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Attachments table
CREATE TABLE IF NOT EXISTS attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email_id UUID REFERENCES emails(id) ON DELETE CASCADE,
    filename TEXT,
    mime_type TEXT,
    size_bytes INTEGER,
    extracted_text TEXT,
    embedding vector(1536),
    embedded_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_emails_date ON emails(date);
CREATE INDEX IF NOT EXISTS idx_emails_sender ON emails(sender);
CREATE INDEX IF NOT EXISTS idx_emails_tier ON emails(tier);
CREATE INDEX IF NOT EXISTS idx_attachments_email ON attachments(email_id);
