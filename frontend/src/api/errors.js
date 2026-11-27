// The API reports problems as RFC 7807 ProblemDetails (see ExceptionMiddleware). Model-binding failures use
// ValidationProblemDetails, which adds an "errors" map such as { "Description": ["..."] }.

export class ApiError extends Error {
  constructor(status, title, detail, fieldErrors = {}) {
    super(detail || title);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.fieldErrors = fieldErrors;
  }
}

/** { "Description": ["too short"], "$.peopleAffected": ["bad"] } -> { description: "too short", peopleAffected: "bad" } */
export function normalizeFieldErrors(errors) {
  const out = {};
  if (!errors || typeof errors !== 'object') return out;
  for (const [rawKey, messages] of Object.entries(errors)) {
    const key = rawKey.replace(/^\$\./, '');
    const field = key ? key.charAt(0).toLowerCase() + key.slice(1) : '_form';
    out[field] = Array.isArray(messages) ? messages.join(' ') : String(messages);
  }
  return out;
}

export function defaultMessageFor(status) {
  switch (status) {
    case 0: return 'Cannot reach the server. Check that the API is running and try again.';
    case 400: return 'Some of the information is not valid.';
    case 401: return 'Your session has expired. Please sign in again.';
    case 403: return 'You do not have permission to do that.';
    case 404: return 'That could not be found.';
    case 409: return 'That conflicts with the current state. Refresh and try again.';
    case 429: return 'Too many attempts. Please wait a minute and try again.';
    default: return status >= 500 ? 'Something went wrong on the server. Please try again.' : 'The request failed.';
  }
}

/** Build an ApiError from an HTTP status and an (optionally empty) parsed JSON body. */
export function toApiError(status, body) {
  const b = body && typeof body === 'object' ? body : {};
  const fieldErrors = normalizeFieldErrors(b.errors);
  const title = typeof b.title === 'string' ? b.title : '';
  let detail = typeof b.detail === 'string' && b.detail ? b.detail : '';
  if (!detail && Object.keys(fieldErrors).length > 0) detail = 'Please fix the highlighted fields.';
  if (!detail) detail = defaultMessageFor(status);
  return new ApiError(status, title || `HTTP ${status}`, detail, fieldErrors);
}
