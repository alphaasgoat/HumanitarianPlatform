import { Link, useNavigate } from 'react-router-dom';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { crisisApi, shelterApi } from '../../api/endpoints.js';
import { buildShelterPayload, validateShelter } from '../../lib/validation.js';
import ShelterFields from '../../components/ShelterFields.jsx';
import { ErrorMessage } from '../../components/ui.jsx';

const ORDER = ['name', 'address', 'crisisEventId', 'latitude', 'longitude', 'capacity', 'notes'];

export default function ShelterForm() {
  const navigate = useNavigate();
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { values, errors, setErrors, change, set } = useForm({
    name: '', address: '', latitude: '', longitude: '', capacity: '50', crisisEventId: '',
    hasMedicalSupport: false, wheelchairAccessible: false, providesFood: true, providesWater: true,
    isPublic: true, notes: ''
  });
  const { busy, error, run } = useAction();

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateShelter(values);
    setErrors(found);
    if (focusFirstError(found, ORDER)) return;
    const result = await run(() => shelterApi.create(buildShelterPayload(values)));
    if (result.ok) navigate(`/staff/shelters/${result.value.id}`);
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <>
      <p><Link to="/staff/shelters">← Back to shelters</Link></p>
      <div className="page-head"><h1>Add a shelter</h1></div>
      <form className="card form-grid" onSubmit={onSubmit} noValidate>
        <ErrorMessage error={error} />
        <ShelterFields values={values} errors={errors} change={change} creating crises={crises.data ?? []} onLocated={set} />
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Create shelter'}</button>
          <Link className="btn" to="/staff/shelters">Cancel</Link>
        </div>
      </form>
    </>
  );
}
