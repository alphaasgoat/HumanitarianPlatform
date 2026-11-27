import { humanize } from '../lib/validation.js';
import LocateButton from './LocateButton.jsx';
import { CheckField, TextField } from './ui.jsx';

/** Fields shared by "add shelter" (creating) and "edit shelter" (name, address, location and crisis are fixed). */
export default function ShelterFields({ values, errors, change, creating, crises = [], onLocated }) {
  return (
    <>
      {creating && (
        <>
          <div className="span-2">
            <TextField id="name" label="Shelter name" value={values.name} onChange={change} error={errors.name} maxLength={150} />
          </div>
          <div className="span-2">
            <TextField id="address" label="Address" value={values.address} onChange={change} error={errors.address} maxLength={300} />
            {onLocated && <LocateButton address={values.address} onFound={onLocated} />}
          </div>
          <TextField id="crisisEventId" label="Crisis (optional)" as="select" value={values.crisisEventId} onChange={change}
            error={errors.crisisEventId} hint="Links the shelter to one crisis event.">
            <option value="">Not linked to a crisis</option>
            {crises.map((c) => <option key={c.id} value={c.id}>{c.name} ({humanize(c.status)})</option>)}
          </TextField>
          <div />
          <TextField id="latitude" label="Latitude (optional)" inputMode="decimal" value={values.latitude} onChange={change}
            error={errors.latitude} hint="Used to suggest the nearest shelter." />
          <TextField id="longitude" label="Longitude (optional)" inputMode="decimal" value={values.longitude} onChange={change}
            error={errors.longitude} />
        </>
      )}
      <TextField id="capacity" label="Capacity (people)" type="number" min="1" max="100000" inputMode="numeric"
        value={values.capacity} onChange={change} error={errors.capacity} />
      <div />
      <fieldset className="span-2 location">
        <legend>What the shelter offers</legend>
        <div className="checks">
          <CheckField id="wheelchairAccessible" label="Wheelchair accessible" checked={values.wheelchairAccessible} onChange={change} />
          <CheckField id="hasMedicalSupport" label="Medical support" checked={values.hasMedicalSupport} onChange={change} />
          <CheckField id="providesFood" label="Provides food" checked={values.providesFood} onChange={change} />
          <CheckField id="providesWater" label="Provides water" checked={values.providesWater} onChange={change} />
          <CheckField id="isPublic" label="Show on the public shelter list" checked={values.isPublic} onChange={change}
            hint="(citizens see availability only, never exact numbers)" />
        </div>
      </fieldset>
      <div className="span-2">
        <TextField id="notes" label="Notes (optional)" as="textarea" rows={3} maxLength={500} value={values.notes}
          onChange={change} error={errors.notes} />
      </div>
    </>
  );
}
