/**
 * WebhookManager — React component for managing webhook subscriptions.
 *
 * This is illustrative source — copy into your React project and provide
 * a SaasBuilderClient instance via props.  No build pipeline is included.
 *
 * Dependencies (install in your app):
 *   react, react-dom, @types/react, @types/react-dom
 */
import React, { useCallback, useEffect, useState } from "react";
import type { SaasBuilderClient } from "../index.js";

// ---------------------------------------------------------------------------
// Types (mirror server-side DTOs)
// ---------------------------------------------------------------------------

interface WebhookEndpoint {
  id: string;
  url: string;
  description: string;
  events: string[];
  isActive: boolean;
  createdAt: string;
}

interface DeliveryLog {
  id: string;
  endpointId: string;
  eventType: string;
  statusCode: number;
  attemptedAt: string;
  succeeded: boolean;
}

interface WebhookManagerProps {
  client: SaasBuilderClient;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function WebhookManager({ client }: WebhookManagerProps): React.ReactElement {
  const [endpoints, setEndpoints] = useState<WebhookEndpoint[]>([]);
  const [deliveries, setDeliveries] = useState<DeliveryLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formUrl, setFormUrl] = useState("");
  const [formDescription, setFormDescription] = useState("");
  const [formEvents, setFormEvents] = useState("");
  const [saving, setSaving] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [eps, logs] = await Promise.all([
        client.request<WebhookEndpoint[]>("/api/v1/webhooks/endpoints"),
        client.request<DeliveryLog[]>("/api/v1/webhooks/deliveries"),
      ]);
      setEndpoints(eps);
      setDeliveries(logs);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load webhooks.");
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const handleSave = async (): Promise<void> => {
    setSaving(true);
    try {
      const payload = {
        url: formUrl,
        description: formDescription,
        events: formEvents.split(",").map((e) => e.trim()).filter(Boolean),
      };
      if (editingId) {
        await client.request(`/api/v1/webhooks/endpoints/${editingId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        });
      } else {
        await client.request("/api/v1/webhooks/endpoints", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        });
      }
      setShowForm(false);
      setEditingId(null);
      setFormUrl("");
      setFormDescription("");
      setFormEvents("");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Save failed.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string): Promise<void> => {
    if (!confirm("Delete this endpoint?")) return;
    try {
      await client.request(`/api/v1/webhooks/endpoints/${id}`, { method: "DELETE" });
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Delete failed.");
    }
  };

  const handleTestSend = async (id: string): Promise<void> => {
    try {
      await client.request(`/api/v1/webhooks/endpoints/${id}/test`, { method: "POST" });
      alert("Test event sent.");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Test send failed.");
    }
  };

  const handleReplay = async (deliveryId: string): Promise<void> => {
    try {
      await client.request(`/api/v1/webhooks/deliveries/${deliveryId}/replay`, {
        method: "POST",
      });
      alert("Replay queued.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Replay failed.");
    }
  };

  const openEditForm = (ep: WebhookEndpoint): void => {
    setEditingId(ep.id);
    setFormUrl(ep.url);
    setFormDescription(ep.description);
    setFormEvents(ep.events.join(", "));
    setShowForm(true);
  };

  if (loading) return <p>Loading webhooks...</p>;

  return (
    <div style={{ fontFamily: "sans-serif", maxWidth: 900 }}>
      <h2>Webhook Endpoints</h2>
      {error && <p style={{ color: "red" }}>{error}</p>}

      <button onClick={() => setShowForm(true)}>+ Add Endpoint</button>

      {showForm && (
        <div style={{ border: "1px solid #ccc", padding: 16, margin: "16px 0" }}>
          <h3>{editingId ? "Edit" : "Add"} Endpoint</h3>
          <div>
            <label>URL<br />
              <input value={formUrl} onChange={(e) => setFormUrl(e.target.value)} style={{ width: 400 }} />
            </label>
          </div>
          <div>
            <label>Description<br />
              <input value={formDescription} onChange={(e) => setFormDescription(e.target.value)} style={{ width: 400 }} />
            </label>
          </div>
          <div>
            <label>Events (comma-separated)<br />
              <input value={formEvents} onChange={(e) => setFormEvents(e.target.value)} style={{ width: 400 }} placeholder="invoice.paid, member.invited" />
            </label>
          </div>
          <button onClick={() => void handleSave()} disabled={saving}>{saving ? "Saving..." : "Save"}</button>
          {" "}
          <button onClick={() => { setShowForm(false); setEditingId(null); }}>Cancel</button>
        </div>
      )}

      <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 16 }}>
        <thead>
          <tr style={{ background: "#f5f5f5" }}>
            <th style={{ textAlign: "left", padding: 8 }}>URL</th>
            <th style={{ textAlign: "left", padding: 8 }}>Description</th>
            <th style={{ textAlign: "left", padding: 8 }}>Events</th>
            <th style={{ textAlign: "left", padding: 8 }}>Status</th>
            <th style={{ padding: 8 }}>Actions</th>
          </tr>
        </thead>
        <tbody>
          {endpoints.map((ep) => (
            <tr key={ep.id} style={{ borderTop: "1px solid #eee" }}>
              <td style={{ padding: 8 }}>{ep.url}</td>
              <td style={{ padding: 8 }}>{ep.description}</td>
              <td style={{ padding: 8 }}>{ep.events.join(", ")}</td>
              <td style={{ padding: 8 }}>{ep.isActive ? "Active" : "Inactive"}</td>
              <td style={{ padding: 8, whiteSpace: "nowrap" }}>
                <button onClick={() => openEditForm(ep)}>Edit</button>{" "}
                <button onClick={() => void handleTestSend(ep.id)}>Test</button>{" "}
                <button onClick={() => void handleDelete(ep.id)} style={{ color: "red" }}>Delete</button>
              </td>
            </tr>
          ))}
          {endpoints.length === 0 && (
            <tr><td colSpan={5} style={{ padding: 16, textAlign: "center" }}>No endpoints configured.</td></tr>
          )}
        </tbody>
      </table>

      <h2 style={{ marginTop: 32 }}>Delivery Log</h2>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr style={{ background: "#f5f5f5" }}>
            <th style={{ textAlign: "left", padding: 8 }}>Event</th>
            <th style={{ textAlign: "left", padding: 8 }}>Status</th>
            <th style={{ textAlign: "left", padding: 8 }}>Attempted</th>
            <th style={{ textAlign: "left", padding: 8 }}>Result</th>
            <th style={{ padding: 8 }}>Actions</th>
          </tr>
        </thead>
        <tbody>
          {deliveries.map((d) => (
            <tr key={d.id} style={{ borderTop: "1px solid #eee" }}>
              <td style={{ padding: 8 }}>{d.eventType}</td>
              <td style={{ padding: 8 }}>{d.statusCode}</td>
              <td style={{ padding: 8 }}>{new Date(d.attemptedAt).toLocaleString()}</td>
              <td style={{ padding: 8 }}>{d.succeeded ? "OK" : "Failed"}</td>
              <td style={{ padding: 8 }}>
                <button onClick={() => void handleReplay(d.id)}>Replay</button>
              </td>
            </tr>
          ))}
          {deliveries.length === 0 && (
            <tr><td colSpan={5} style={{ padding: 16, textAlign: "center" }}>No deliveries yet.</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
