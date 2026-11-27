import { Link, useNavigate } from 'react-router-dom';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { resourceApi } from '../../api/endpoints.js';
import { SUPPLY_CATEGORIES, buildResourcePayload, validateResource } from '../../lib/validation.js';
import { ErrorMessage, TextField } from '../../components/ui.jsx';

const ORDER = ['name', 'category', 'unit', 'quantityTotal', 'reorderLevel', 'locationName', 'latitude', 'longitude'];

export default function ResourceForm() {
  const navigate = useNavigate();
  const { values, errors, setErrors, change } = useForm({
    name: '', category: 'Food', unit: 'packs', quantityTotal: '100', reorderLevel: '20',
    locationName: '', latitude: '', longitude: ''
  });
  const { busy, error, run } = useAction();

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateResource(values);
    setErrors(found);
    if (focusFirstError(found, ORDER)) return;
    const result = await run(() => resourceApi.create(buildResourcePayload(values)));
    if (result.ok) navigate('/staff/resources');
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <>
      <p><Link to="/staff/resources">← Back to resources</Link></p>
      <div className="page-head"><h1>Add a stock item</h1></div>
      <form className="card form-grid" onSubmit={onSubmit} noValidate>
        <ErrorMessage error={error} />
        <div className="span-2">
          <TextField id="name" label="Item name" value={values.name} onChange={change} error={errors.name} maxLength={150}
            placeholder="e.g. Dry ration packs" />
        </div>
        <TextField id="category" label="Category" as="select" value={values.category} onChange={change} error={errors.category}>
          {SUPPLY_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </TextField>
        <TextField id="unit" label="Unit" value={values.unit} onChange={change} error={errors.unit} maxLength={30}
          hint="packs, litres, boxes, vehicles ..." />
        <TextField id="quantityTotal" label="Quantity in stock" type="number" min="0" inputMode="numeric"
          value={values.quantityTotal} onChange={change} error={errors.quantityTotal} />
        <TextField id="reorderLevel" label="Reorder level" type="number" min="0" inputMode="numeric"
          value={values.reorderLevel} onChange={change} error={errors.reorderLevel}
          hint="The item is flagged as low stock at or below this number." />
        <div className="span-2">
          <TextField id="locationName" label="Where is it stored? (optional)" value={values.locationName} onChange={change}
            error={errors.locationName} maxLength={200} />
        </div>
        <TextField id="latitude" label="Latitude (optional)" inputMode="decimal" value={values.latitude} onChange={change}
          error={errors.latitude} hint="Used to suggest the nearest stock." />
        <TextField id="longitude" label="Longitude (optional)" inputMode="decimal" value={values.longitude} onChange={change} error={errors.longitude} />
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Add item'}</button>
          <Link className="btn" to="/staff/resources">Cancel</Link>
        </div>
      </form>
    </>
  );
}
