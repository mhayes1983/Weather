import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sourceDescription, sourceTitle } from '../src/sourceDescriptions.ts';

test('every Open-Meteo model and source has a description', () => {
    for (const id of ['ecmwf', 'ecmwf-aifs', 'gfs', 'nbm', 'hrrr', 'gem', 'hrdps', 'icon', 'ukmo', 'jma', 'meteofrance', 'cma',
        'weathernext', 'nws', 'wu', 'weatherapi', 'ncm-coastal', 'ncm-inland', 'ncm', 'wmtw', 'wgme']) {
        assert.ok(sourceDescription(id), id);
    }
});

test('added manual rows are described, and unknown sources have none', () => {
    assert.equal(sourceDescription('manual-abc'), 'Typed in by hand.');
    assert.equal(sourceDescription('unknown'), undefined);
});

test('titles put the description before status details, skipping blanks', () => {
    assert.equal(sourceTitle('weatherapi', 'Needs key', null), "WAPI: WeatherAPI.com's forecast. The free plan covers 3 days.\nNeeds key");
    assert.equal(sourceTitle('unknown', null), undefined);
});

test('each description starts with the short name shown in the tables', () => {
    const names: Record<string, string> = {
        ecmwf: 'ECMWF', 'ecmwf-aifs': 'AIFS', gfs: 'GFS', nbm: 'NBM', hrrr: 'HRRR', gem: 'GEM', hrdps: 'HRDPS', icon: 'ICON',
        ukmo: 'UKMO', jma: 'JMA', meteofrance: 'MF', cma: 'CMA', weathernext: 'WN', nws: 'NWS', wu: 'WU', weatherapi: 'WAPI',
        'ncm-coastal': 'NCM-C', 'ncm-inland': 'NCM-I', ncm: 'NCM', wmtw: 'WMTW', wgme: 'WGME',
    };
    for (const [id, name] of Object.entries(names)) {
        assert.ok(sourceDescription(id)?.startsWith(name), id);
    }
});
