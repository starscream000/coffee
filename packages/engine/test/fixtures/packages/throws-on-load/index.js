// Test fixture: a package whose own code throws while it loads, so the load
// error's hint must point at the package, not at the action file.
'use strict';

throw new Error('throws-on-load failed to start');
