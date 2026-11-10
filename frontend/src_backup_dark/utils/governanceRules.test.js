import test from 'node:test';
import assert from 'node:assert/strict';
import {validateChangeReview} from './governanceRules.js';

// FALSE
test('reject without a note is refused', () => assert.match(validateChangeReview('Rejected',''), /at least 10/));
test('reject with spaces only is refused', () => assert.match(validateChangeReview('Rejected','         '), /at least 10/));
test('reject with 9 characters is refused', () => assert.match(validateChangeReview('Rejected','123456789'), /at least 10/));
test('note over 1000 characters is refused', () => assert.match(validateChangeReview('Approved','x'.repeat(1001)), /at most 1000/));
test('unknown decision is refused', () => assert.equal(validateChangeReview('Maybe','long enough note'), 'Select a decision.'));
// TRUE
test('approve without a note is accepted', () => assert.equal(validateChangeReview('Approved',''), ''));
test('approve with a note is accepted', () => assert.equal(validateChangeReview('Approved','Looks fine'), ''));
test('reject with 10 characters is accepted', () => assert.equal(validateChangeReview('Rejected','1234567890'), ''));
test('reject with 1000 characters is accepted', () => assert.equal(validateChangeReview('Rejected','x'.repeat(1000)), ''));
