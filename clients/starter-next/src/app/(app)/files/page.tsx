import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import type { Metadata } from "next";
import { FileText, Download } from "lucide-react";
import { apiGet } from "@/lib/api";
import { FileUploader } from "@/components/file-uploader";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

export const metadata: Metadata = { title: "Files" };

interface FileRecord {
  id: string;
  name: string;
  size: number;
  contentType: string;
  uploadedAt: string;
  downloadUrl: string;
}

interface FilesResponse {
  items: FileRecord[];
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default async function FilesPage(): Promise<React.JSX.Element> {
  const jar = await cookies();
  const token = jar.get("sb_token")?.value;
  if (!token) redirect("/login");

  const files = await apiGet<FilesResponse>("/api/v1/files", token).catch(
    () => ({ items: [] }),
  );

  return (
    <div className="space-y-6">
      <FileUploader accept="*/*" maxSizeMb={50} />

      {files.items.length === 0 ? (
        <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
          <FileText className="mx-auto mb-2 h-8 w-8 opacity-40" />
          No files uploaded yet.
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Size</TableHead>
              <TableHead>Uploaded</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {files.items.map((file) => (
              <TableRow key={file.id}>
                <TableCell className="font-medium">{file.name}</TableCell>
                <TableCell className="text-muted-foreground text-sm">
                  {file.contentType}
                </TableCell>
                <TableCell className="text-muted-foreground text-sm">
                  {formatBytes(file.size)}
                </TableCell>
                <TableCell className="text-muted-foreground text-sm">
                  {new Date(file.uploadedAt).toLocaleDateString()}
                </TableCell>
                <TableCell>
                  <Button variant="ghost" size="icon" asChild>
                    <a
                      href={file.downloadUrl}
                      download={file.name}
                      aria-label={`Download ${file.name}`}
                    >
                      <Download className="h-4 w-4" />
                    </a>
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
