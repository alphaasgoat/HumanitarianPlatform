import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { resourceApi } from '../../api/endpoints.js';
import { SUPPLY_CATEGORIES } from '../../lib/validation.js';
import PromptButton from '../../components/PromptButton.jsx';
import { Badge, EmptyState, ErrorMessage, Meter, Spinner, StatTile } from '../../components/ui.jsx';

export default function Resources() {
  const { hasRole } = useAuth();
  const canManage = hasRole(GROUPS.resourceManage);
  const [category, setCategory] = useState('');
  const [lowOnly, setLowOnly] = useState(false);
  const { data, error, loading, reload } = useApi(
    (signal) => resourceApi.list({ category, lowStockOnly: lowOnly }, signal), [category, lowOnly]
  );
  const action = useAction();

  const items = data ?? [];
  const low = items.filter((r) => r.isLowStock).length;

  async function toggle(r) {
    const result = await action.run(() => resourceApi.setActive(r.id, !r.isActive));
    if (result.ok) reload();
  }

  return (
    <>
      <div className="page-head">
        <h1>Resources</h1>
        {canManage && <Link className="btn btn-primary" to="/staff/resources/new">Add item</Link>}
      </div>

      <div className="toolbar">
        <div>
          <label htmlFor="category">Category</label>
          <select id="category" value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All</option>
            {SUPPLY_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <label className="inline-check">
          <input type="checkbox" checked={lowOnly} onChange={(e) => setLowOnly(e.target.checked)} /> Low stock only
        </label>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}

      {data && items.length > 0 && (
        <div className="stat-grid">
          <StatTile label="Items" value={items.length} />
          <StatTile label="Low stock" value={low} hint={low > 0 ? 'needs replenishing' : 'all above reorder level'} />
          <StatTile label="Inactive" value={items.filter((r) => !r.isActive).length} />
        </div>
      )}
      {data && items.length === 0 && (
        <EmptyState title="No items match.">{canManage ? 'Add stock items so supplies can be allocated to requests.' : ''}</EmptyState>
      )}

      {data && items.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Item</th><th>Category</th><th>Available</th><th>In use</th><th>Reorder at</th><th>Location</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id}>
                  <td><strong>{r.name}</strong></td>
                  <td>{r.category}</td>
                  <td>{r.quantityAvailable} / {r.quantityTotal} <span className="muted">{r.unit}</span></td>
                  <td className="meter-cell"><Meter percent={r.utilisationPercent} label={`${r.utilisationPercent}% in use`} /></td>
                  <td>{r.reorderLevel}{r.isLowStock && <span className="badge allocation-Cancelled low-flag">Low stock</span>}</td>
                  <td className="wrap">{r.locationName ?? '—'}</td>
                  <td><Badge kind="active" value={r.isActive ? 'Active' : 'Inactive'} /></td>
                  <td>
                    {canManage && (
                      <div className="actions">
                        <PromptButton label="Restock" confirmLabel="Add stock" placeholder="Quantity to add" type="number" required
                          inputProps={{ min: 1, step: 1 }}
                          onSubmit={async (qty) => { await resourceApi.restock(r.id, qty, null); reload(); }} />
                        <button type="button" className="btn btn-small" disabled={action.busy} onClick={() => toggle(r)}>
                          {r.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
