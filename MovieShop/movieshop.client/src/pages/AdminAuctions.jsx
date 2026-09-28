import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Eye, Gavel, Pencil, Plus, Save, Trash2, X } from 'lucide-react';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import { formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Spinner } from '@/components/ui/spinner';
import { PageContainer, PageHeader } from '@/components/ui/page';

const statusLabel = { 0: 'Pending', 1: 'Active', 2: 'Ended' };
const statusVariant = { 0: 'warning', 1: 'success', 2: 'muted' };

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
        window.scrollTo({ top: 0, behavior: 'smooth' });
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
        <PageContainer size="xl">
            <PageHeader title="Manage Auctions" icon={Gavel} />

            {/* Create / Edit form */}
            <Card className={isEditing ? "mb-8 border-primary/50" : "mb-8"}>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        {isEditing ? <><Pencil className="size-4 text-primary" />Edit: {editTarget.title}</> : <><Plus className="size-4 text-primary" />Create New Auction</>}
                    </CardTitle>
                    {isEditing && (
                        <CardAction>
                            <Button variant="outline" size="sm"
                                onClick={() => { setEditTarget(null); setForm(emptyForm); setMsg(null); }}>
                                <X /> Cancel edit
                            </Button>
                        </CardAction>
                    )}
                </CardHeader>
                <CardContent>
                    <form onSubmit={isEditing ? handleEdit : handleCreate}>
                        <div className="grid gap-5 md:grid-cols-[1fr_1fr_140px]">
                            <div className="space-y-2">
                                <Label htmlFor="auction-movie">Movie (optional)</Label>
                                <NativeSelect id="auction-movie" name="movieId" value={form.movieId} onChange={handleChange}>
                                    <option value="">— Standalone item —</option>
                                    {movies.map(m => <option key={m.id} value={m.id}>{m.title}</option>)}
                                </NativeSelect>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="auction-title">Auction Title</Label>
                                <Input id="auction-title" name="auctionTitle" value={form.auctionTitle}
                                    onChange={handleChange} placeholder="e.g. The One Ring — Original Prop" />
                            </div>
                            <div className="row-span-3 hidden md:block">
                                <Label className="mb-2">Preview</Label>
                                <div className="aspect-[2/3] overflow-hidden rounded-lg border bg-muted">
                                    {form.imageUrl && <img src={form.imageUrl} alt="preview" className="size-full object-cover" />}
                                </div>
                            </div>
                            <div className="space-y-2 md:col-span-2">
                                <Label htmlFor="auction-description">Description</Label>
                                <Textarea id="auction-description" name="description" value={form.description}
                                    onChange={handleChange} rows={2} />
                            </div>
                            <div className="space-y-2 md:col-span-2">
                                <Label htmlFor="auction-image">Image URL</Label>
                                <Input id="auction-image" name="imageUrl" value={form.imageUrl}
                                    onChange={handleChange} placeholder="https://..." />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="auction-starts">Starts At</Label>
                                <Input id="auction-starts" type="datetime-local" name="startsAt"
                                    value={form.startsAt} onChange={handleChange} required />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="auction-ends">Ends At</Label>
                                <Input id="auction-ends" type="datetime-local" name="endsAt"
                                    value={form.endsAt} onChange={handleChange} required />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="auction-price">
                                    Starting Price (Ft)
                                </Label>
                                <Input id="auction-price" type="number" name="startingPrice"
                                    value={form.startingPrice} onChange={handleChange} min={1} required
                                    disabled={isEditing && editTarget.status !== 0} />
                                {isEditing && editTarget.status !== 0 && (
                                    <p className="text-xs text-muted-foreground">(locked — auction active)</p>
                                )}
                            </div>
                        </div>

                        {msg && (
                            <Alert variant={msg.type === 'success' ? 'success' : 'destructive'} className="mt-5">
                                {msg.type === 'success' ? <CheckCircle2 /> : <AlertCircle />}
                                <AlertDescription>{msg.text}</AlertDescription>
                            </Alert>
                        )}

                        <Button type="submit" className="mt-5" disabled={saving}>
                            {saving ? <><Spinner />Saving…</> : isEditing ? <><Save />Save Changes</> : <><Plus />Create Auction</>}
                        </Button>
                    </form>
                </CardContent>
            </Card>

            {/* Auctions table */}
            <Card className="py-0">
                <Table>
                    <TableHeader>
                        <TableRow className="hover:bg-transparent">
                            <TableHead className="w-14">ID</TableHead>
                            <TableHead className="w-20">Image</TableHead>
                            <TableHead>Title</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Current Bid</TableHead>
                            <TableHead>Ends At</TableHead>
                            <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {auctions.length === 0 && (
                            <TableRow>
                                <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">No auctions yet.</TableCell>
                            </TableRow>
                        )}
                        {auctions.map(a => (
                            <TableRow key={a.id}>
                                <TableCell className="text-muted-foreground">{a.id}</TableCell>
                                <TableCell>
                                    {a.imageUrl && (
                                        <img src={a.imageUrl} alt="" className="h-10 w-16 rounded object-cover ring-1 ring-border" />
                                    )}
                                </TableCell>
                                <TableCell className="font-medium">{a.title}</TableCell>
                                <TableCell>
                                    <Badge variant={statusVariant[a.status]}>{statusLabel[a.status]}</Badge>
                                </TableCell>
                                <TableCell className="whitespace-nowrap">{formatPrice(a.currentPrice)}</TableCell>
                                <TableCell className="text-xs whitespace-nowrap text-muted-foreground">{new Date(a.endsAt).toLocaleString()}</TableCell>
                                <TableCell>
                                    <div className="flex justify-end gap-1">
                                        <Button variant="ghost" size="sm" asChild>
                                            <Link to={`/auctions/${a.id}`}><Eye />View</Link>
                                        </Button>
                                        <Button variant="ghost" size="sm" onClick={() => openEdit(a)}><Pencil />Edit</Button>
                                        <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => setDeleteId(a.id)}>
                                            <Trash2 />Delete
                                        </Button>
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </Card>

            {/* Delete confirm modal */}
            <Dialog open={deleteId !== null} onOpenChange={(open) => { if (!open) setDeleteId(null); }}>
                <DialogContent className="sm:max-w-sm">
                    <DialogHeader>
                        <DialogTitle>Delete this auction?</DialogTitle>
                        <DialogDescription>This cannot be undone.</DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDeleteId(null)}>Cancel</Button>
                        <Button variant="destructive" onClick={handleDelete}><Trash2 />Delete</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </PageContainer>
    );
}
