import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';

const statusLabel = { 0: 'Pending', 1: 'Active', 2: 'Ended' };

const emptyForm = {
    movieId: '', auctionTitle: '', description: '', imageUrl: '',
    startsAt: '', endsAt: '', startingPrice: ''
};

function toLocalInput(isoStr) {
    if (!isoStr) return '';
    const d = new Date(isoStr);
    // datetime-local format: YYYY-MM-DDTHH:MM
    return d.toISOString().slice(0, 16);
}

export default function AdminAuctions() {
    const { token } = useAuth();
    const [auctions, setAuctions] = useState([]);
    const [movies, setMovies] = useState([]);
    const [form, setForm] = useState(emptyForm);
    const [editTarget, setEditTarget] = useState(null); // auction being edited
    const [saving, setSaving] = useState(false);
    const [msg, setMsg] = useState(null);
    const [deleteId, setDeleteId] = useState(null); // confirm modal

    useEffect(() => {
        fetch(`${API_BASE_URL}/api/Auction/all`, {
            headers: { Authorization: `Bearer ${token}` }
        }).then(r => r.ok ? r.json() : []).then(setAuctions);
        fetch(`${API_BASE_URL}/api/Movie`).then(r => r.json()).then(d => setMovies(d.items ?? d));
    }, [token]);

    const handleChange = e => setForm(prev => ({ ...prev, [e.target.name]: e.target.value }));

    const buildBody = () => ({
        movieId:       form.movieId ? parseInt(form.movieId) : null,
        auctionTitle:  form.auctionTitle || null,
        description:   form.description || null,
        imageUrl:      form.imageUrl || null,
        startsAt:      new Date(form.startsAt).toISOString(),
        endsAt:        new Date(form.endsAt).toISOString(),
        startingPrice: parseFloat(form.startingPrice)
    });

    const handleCreate = async e => {
        e.preventDefault();
        setSaving(true); setMsg(null);
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify(buildBody())
            });
            if (r.ok) {
                const created = await r.json();
                setAuctions(prev => [created, ...prev]);
                setForm(emptyForm);
                setMsg({ type: 'success', text: `Auction "${created.title}" created!` });
            } else {
                setMsg({ type: 'error', text: await r.text() });
            }
        } catch { setMsg({ type: 'error', text: 'Network error.' }); }
        finally { setSaving(false); }
    };

    const openEdit = (a) => {
        setEditTarget(a);
        setForm({
            movieId:       a.movieId ?? '',
            auctionTitle:  a.title ?? '',
            description:   a.description ?? '',
            imageUrl:      a.imageUrl ?? '',
            startsAt:      toLocalInput(a.startsAt),
            endsAt:        toLocalInput(a.endsAt),
            startingPrice: a.startingPrice
        });
        setMsg(null);
    };

    const handleEdit = async e => {
        e.preventDefault();
        setSaving(true); setMsg(null);
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${editTarget.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify(buildBody())
            });
            if (r.ok) {
                const updated = await r.json();
                setAuctions(prev => prev.map(a => a.id === updated.id ? updated : a));
                setEditTarget(null);
                setForm(emptyForm);
                setMsg({ type: 'success', text: `Auction updated!` });
            } else {
                setMsg({ type: 'error', text: await r.text() });
            }
        } catch { setMsg({ type: 'error', text: 'Network error.' }); }
        finally { setSaving(false); }
    };

    const handleDelete = async () => {
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${deleteId}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (r.ok) setAuctions(prev => prev.filter(a => a.id !== deleteId));
        } catch { /* ignore */ }
        finally { setDeleteId(null); }
    };

    const isEditing = editTarget !== null;

    return (
        <div style={{ maxWidth: 960, margin: '0 auto', padding: '30px 20px' }}>
            <h2>🎬 Manage Auctions</h2>

            {/* Create / Edit form */}
            <div className="card mb-4">
                <div className="card-header fw-bold d-flex justify-content-between align-items-center">
                    <span>{isEditing ? `✏️ Edit: ${editTarget.title}` : 'Create New Auction'}</span>
                    {isEditing && (
                        <button className="btn btn-sm btn-outline-secondary"
                            onClick={() => { setEditTarget(null); setForm(emptyForm); setMsg(null); }}>
                            ✕ Cancel edit
                        </button>
                    )}
                </div>
                <div className="card-body">
                    <form onSubmit={isEditing ? handleEdit : handleCreate}>
                        <div className="row g-3">
                            <div className="col-md-6">
                                <label className="form-label">Movie (optional)</label>
                                <select className="form-select" name="movieId" value={form.movieId} onChange={handleChange}>
                                    <option value="">— Standalone item —</option>
                                    {movies.map(m => <option key={m.id} value={m.id}>{m.title}</option>)}
                                </select>
                            </div>
                            <div className="col-md-6">
                                <label className="form-label">Auction Title</label>
                                <input className="form-control" name="auctionTitle" value={form.auctionTitle}
                                    onChange={handleChange} placeholder="e.g. The One Ring — Original Prop" />
                            </div>
                            <div className="col-12">
                                <label className="form-label">Description</label>
                                <textarea className="form-control" name="description" value={form.description}
                                    onChange={handleChange} rows={2} />
                            </div>
                            <div className="col-12">
                                <label className="form-label">Image URL</label>
                                <input className="form-control" name="imageUrl" value={form.imageUrl}
                                    onChange={handleChange} placeholder="https://..." />
                                {form.imageUrl && (
                                    <img src={form.imageUrl} alt="preview"
                                        style={{ height: 80, marginTop: 8, borderRadius: 6, objectFit: 'cover' }} />
                                )}
                            </div>
                            <div className="col-md-6">
                                <label className="form-label">Starts At</label>
                                <input type="datetime-local" className="form-control" name="startsAt"
                                    value={form.startsAt} onChange={handleChange} required />
                            </div>
                            <div className="col-md-6">
                                <label className="form-label">Ends At</label>
                                <input type="datetime-local" className="form-control" name="endsAt"
                                    value={form.endsAt} onChange={handleChange} required />
                            </div>
                            <div className="col-md-4">
                                <label className="form-label">
                                    Starting Price (Ft)
                                    {isEditing && editTarget.status !== 0 && (
                                        <span className="text-muted ms-1" style={{ fontSize: 11 }}>(locked — auction active)</span>
                                    )}
                                </label>
                                <input type="number" className="form-control" name="startingPrice"
                                    value={form.startingPrice} onChange={handleChange} min={1} required
                                    disabled={isEditing && editTarget.status !== 0} />
                            </div>
                        </div>

                        {msg && (
                            <div className={`alert mt-3 ${msg.type === 'success' ? 'alert-success' : 'alert-danger'}`}>
                                {msg.text}
                            </div>
                        )}

                        <button type="submit" className={`btn mt-3 ${isEditing ? 'btn-warning' : 'btn-primary'}`}
                            disabled={saving}>
                            {saving ? 'Saving…' : isEditing ? '💾 Save Changes' : '+ Create Auction'}
                        </button>
                    </form>
                </div>
            </div>

            {/* Auctions table */}
            <table className="table table-hover">
                <thead className="table-dark">
                    <tr>
                        <th>ID</th><th>Image</th><th>Title</th><th>Status</th>
                        <th>Current Bid</th><th>Ends At</th><th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    {auctions.map(a => (
                        <tr key={a.id}>
                            <td>{a.id}</td>
                            <td>
                                {a.imageUrl && (
                                    <img src={a.imageUrl} alt="" style={{ height: 40, width: 60, objectFit: 'cover', borderRadius: 4 }} />
                                )}
                            </td>
                            <td>{a.title}</td>
                            <td>
                                <span className={`badge bg-${a.status === 1 ? 'success' : a.status === 0 ? 'warning text-dark' : 'secondary'}`}>
                                    {statusLabel[a.status]}
                                </span>
                            </td>
                            <td>{a.currentPrice?.toLocaleString()} Ft</td>
                            <td style={{ fontSize: 12 }}>{new Date(a.endsAt).toLocaleString()}</td>
                            <td>
                                <div className="d-flex gap-1">
                                    <Link to={`/auctions/${a.id}`} className="btn btn-sm btn-outline-primary">View</Link>
                                    <button className="btn btn-sm btn-outline-warning" onClick={() => openEdit(a)}>Edit</button>
                                    <button className="btn btn-sm btn-outline-danger" onClick={() => setDeleteId(a.id)}>Delete</button>
                                </div>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            {/* Delete confirm modal */}
            {deleteId !== null && (
                <div className="modal d-block" style={{ background: 'rgba(0,0,0,0.5)' }}>
                    <div className="modal-dialog modal-sm modal-dialog-centered">
                        <div className="modal-content">
                            <div className="modal-body text-center py-4">
                                <p className="fw-bold">Delete this auction?</p>
                                <p className="text-muted" style={{ fontSize: 13 }}>This cannot be undone.</p>
                                <div className="d-flex gap-2 justify-content-center">
                                    <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
                                    <button className="btn btn-secondary" onClick={() => setDeleteId(null)}>Cancel</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
