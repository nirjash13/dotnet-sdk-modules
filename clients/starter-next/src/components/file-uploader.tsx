"use client";

import * as React from "react";
import { Upload, FileText, CheckCircle2, XCircle } from "lucide-react";
import { apiPost } from "@/lib/api";
import { Button } from "@/components/ui/button";

interface UploadUrlResponse {
  uploadUrl: string;
  fileId: string;
}

interface UploadState {
  file: File;
  status: "pending" | "uploading" | "done" | "error";
  progress: number;
  error?: string;
}

interface FileUploaderProps {
  onUploaded?: (fileId: string, fileName: string) => void;
  accept?: string;
  maxSizeMb?: number;
}

export function FileUploader({
  onUploaded,
  accept = "*/*",
  maxSizeMb = 50,
}: FileUploaderProps): React.JSX.Element {
  const [uploads, setUploads] = React.useState<UploadState[]>([]);
  const inputRef = React.useRef<HTMLInputElement>(null);

  function updateUpload(index: number, patch: Partial<UploadState>): void {
    setUploads((prev) =>
      prev.map((u, i) => (i === index ? { ...u, ...patch } : u)),
    );
  }

  async function handleFiles(files: FileList): Promise<void> {
    const newUploads: UploadState[] = Array.from(files).map((file) => ({
      file,
      status: "pending",
      progress: 0,
    }));
    const baseIndex = uploads.length;
    setUploads((prev) => [...prev, ...newUploads]);

    for (let i = 0; i < newUploads.length; i++) {
      const { file } = newUploads[i];
      const idx = baseIndex + i;

      if (file.size > maxSizeMb * 1024 * 1024) {
        updateUpload(idx, {
          status: "error",
          error: `Exceeds ${maxSizeMb} MB limit`,
        });
        continue;
      }

      updateUpload(idx, { status: "uploading" });

      try {
        const { uploadUrl, fileId } = await apiPost<UploadUrlResponse>(
          "/api/v1/files/upload-url",
          { fileName: file.name, contentType: file.type, size: file.size },
        );

        await fetch(uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });

        updateUpload(idx, { status: "done", progress: 100 });
        onUploaded?.(fileId, file.name);
      } catch (err) {
        updateUpload(idx, {
          status: "error",
          error: err instanceof Error ? err.message : "Upload failed",
        });
      }
    }
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>): void {
    e.preventDefault();
    if (e.dataTransfer.files.length > 0) {
      void handleFiles(e.dataTransfer.files);
    }
  }

  return (
    <div className="space-y-4">
      <div
        role="button"
        tabIndex={0}
        aria-label="Drop files here or click to upload"
        className="flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-muted-foreground/30 px-6 py-12 text-center transition-colors hover:border-primary/50 hover:bg-accent/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        onDrop={handleDrop}
        onDragOver={(e) => e.preventDefault()}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => e.key === "Enter" && inputRef.current?.click()}
      >
        <Upload className="mb-4 h-8 w-8 text-muted-foreground" />
        <p className="text-sm font-medium">Drop files here or click to browse</p>
        <p className="mt-1 text-xs text-muted-foreground">
          Max {maxSizeMb} MB per file
        </p>
        <input
          ref={inputRef}
          type="file"
          accept={accept}
          multiple
          className="sr-only"
          aria-hidden
          onChange={(e) => {
            if (e.target.files) void handleFiles(e.target.files);
          }}
        />
      </div>

      {uploads.length > 0 && (
        <ul className="space-y-2" aria-label="Upload queue">
          {uploads.map(({ file, status, error }, i) => (
            <li
              key={i}
              className="flex items-center gap-3 rounded-md border px-3 py-2 text-sm"
            >
              <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
              <span className="flex-1 truncate">{file.name}</span>
              {status === "uploading" && (
                <span className="text-xs text-muted-foreground">Uploading...</span>
              )}
              {status === "done" && (
                <CheckCircle2 className="h-4 w-4 text-green-500" />
              )}
              {status === "error" && (
                <span className="flex items-center gap-1 text-xs text-destructive">
                  <XCircle className="h-4 w-4" />
                  {error}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {uploads.some((u) => u.status === "done" || u.status === "error") && (
        <Button
          variant="ghost"
          size="sm"
          onClick={() =>
            setUploads((prev) =>
              prev.filter((u) => u.status === "uploading" || u.status === "pending"),
            )
          }
        >
          Clear completed
        </Button>
      )}
    </div>
  );
}
