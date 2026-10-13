const $ = s => document.querySelector(s);

const statuses = [
    'Discovered',
    'Shortlisted',
    'Prepared',
    'Approved',
    'Applied',
    'Interview',
    'Offer',
    'Rejected'
];

status.innerHTML += statuses
    .map(s => `<option>${s}</option>`)
    .join('');


async function api(url, opt) {
    const response = await fetch(url, opt);

    if (!response.ok) {
        throw new Error(
            `${response.status} ${await response.text()}`
        );
    }

    if (response.status === 204)
        return;

    return response.json();
}


function esc(value = '') {
    return String(value).replace(
        /[&<>"]/g,
        c => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;'
        })[c]
    );
}


// ================================================================
// JOB LISTE
// ================================================================

async function load() {
    const q = encodeURIComponent($('#q').value);
    const score = $('#score').value;
    const statusValue = encodeURIComponent($('#status').value);

    const jobs = await api(
        `/api/jobs?q=${q}&minScore=${score}&status=${statusValue}`
    );

    $('#jobs').innerHTML =
        jobs.map(x => {
            const j = x.job;
            const m = x.match;

            const salary = j.salaryMin
                ? ` · ${esc(j.currency)} ${j.salaryMin.toLocaleString()}–${j.salaryMax?.toLocaleString() ?? '?'}`
                : '';

            return `
                <article class="job">

                    <div class="jobtop">

                        <div>
                            <h3>${esc(j.title)}</h3>

                            <div class="meta">
                                ${esc(j.company)}
                                · ${esc(j.location || 'Ort nicht angegeben')}
                                · ${esc(j.source)}
                                ${salary}
                            </div>
                        </div>

                        <div class="score">
                            ${m.score}%
                        </div>

                    </div>

                    <div class="tags">

                        ${m.matchingSkills
                            .map(skill =>
                                `<span class="tag">
                                    ✓ ${esc(skill)}
                                </span>`
                            )
                            .join('')}

                        ${m.missingSkills
                            .map(skill =>
                                `<span class="tag missing">
                                    △ ${esc(skill)}
                                </span>`
                            )
                            .join('')}

                    </div>

                    <div class="actions">

                        <button
                            onclick='showJob(${JSON.stringify(j.externalId)})'>
                            Analyse
                        </button>

                        <a
                            href="/api/job/cv?id=${encodeURIComponent(j.externalId)}&format=pdf">
                            <button class="secondary">
                                CV PDF
                            </button>
                        </a>

                        <a
                            href="/api/job/cv?id=${encodeURIComponent(j.externalId)}">
                            <button class="secondary">
                                CV DOCX
                            </button>
                        </a>

                        <a
                            href="/api/job/cover-letter?id=${encodeURIComponent(j.externalId)}&format=pdf">
                            <button class="secondary">
                                Anschreiben PDF
                            </button>
                        </a>

                        <a
                            href="/api/job/cover-letter?id=${encodeURIComponent(j.externalId)}">
                            <button class="secondary">
                                Anschreiben DOCX
                            </button>
                        </a>

                        <select
                            onchange='setStatus(
                                ${JSON.stringify(j.externalId)},
                                this.value
                            )'>

                            ${statuses
                                .map(s =>
                                    `<option
                                        ${s === x.status ? 'selected' : ''}>
                                        ${s}
                                    </option>`
                                )
                                .join('')}

                        </select>

                        <a
                            target="_blank"
                            rel="noopener noreferrer"
                            href="${esc(j.sourceUrl)}">

                            <button class="secondary">
                                Original
                            </button>

                        </a>

                    </div>

                </article>
            `;
        }).join('') ||
        '<p>Keine Jobs. Klicke „Jobs aktualisieren“.</p>';

    await stats();
}


// ================================================================
// STATISTIK
// ================================================================

async function stats() {
    const data = await api('/api/stats');

    $('#stats').innerHTML = [
        [
            'Gefunden',
            Object.values(data)
                .reduce((a, b) => a + b, 0)
        ],
        [
            'Vorbereitet',
            data.Prepared || 0
        ],
        [
            'Beworben',
            data.Applied || 0
        ],
        [
            'Interviews',
            data.Interview || 0
        ]
    ]
        .map(x => `
            <div class="stat">
                <strong>${x[1]}</strong>
                ${x[0]}
            </div>
        `)
        .join('');
}


// ================================================================
// REQUIREMENTS
// ================================================================

function renderRequirement(requirement) {
    const status = Number(requirement.status);

    let symbol;
    let cssClass;

    switch (status) {
        case 0:
            symbol = '✓';
            cssClass = 'requirement-matched';
            break;

        case 1:
            symbol = '△';
            cssClass = 'requirement-missing';
            break;

        default:
            symbol = '?';
            cssClass = 'requirement-uncertain';
            break;
    }

    return `
        <div class="requirement ${cssClass}">
            <div class="requirement-title">
                <strong>
                    ${symbol} ${esc(requirement.name)}
                </strong>
            </div>

            <div class="requirement-reason">
                ${esc(requirement.reason)}
            </div>
        </div>
    `;
}


function renderRequirements(requirements, status) {
    const filtered = requirements.filter(
        requirement =>
            Number(requirement.status) === status
    );

    if (!filtered.length)
        return '<p>—</p>';

    return filtered
        .map(renderRequirement)
        .join('');
}


// ================================================================
// JOB ANALYSE
// ================================================================

async function showJob(id) {
    try {
        const encodedId = encodeURIComponent(id);

        const [x, requirements] =
            await Promise.all([
                api(
                    '/api/job?id=' +
                    encodedId
                ),

                api(
                    '/api/job/requirements?id=' +
                    encodedId
                )
            ]);

        const j = x.job;
        const m = x.match;

        const salary =
            j.salaryMin
                ? `${esc(j.currency)} ${j.salaryMin.toLocaleString()}–${j.salaryMax?.toLocaleString() ?? '?'}`
                : 'Nicht angegeben';


        const matchedCount =
            requirements.filter(
                r => Number(r.status) === 0
            ).length;

        const missingCount =
            requirements.filter(
                r => Number(r.status) === 1
            ).length;

        const uncertainCount =
            requirements.filter(
                r => Number(r.status) === 2
            ).length;


        $('#detailBody').innerHTML = `

            <div class="analysis-header">

                <div>

                    <h2>
                        ${esc(j.title)}
                    </h2>

                    <p class="meta">
                        <strong>
                            ${esc(j.company)}
                        </strong>

                        · ${esc(
                            j.location ||
                            'Ort nicht angegeben'
                        )}

                        · ${esc(j.source)}
                    </p>

                </div>


                <div class="analysis-score">
                    ${m.score}%
                </div>

            </div>


            <hr>


            <h3>
                Anforderungsanalyse
            </h3>


            <p>
                <strong>${matchedCount}</strong>
                Anforderungen belegt ·

                <strong>${missingCount}</strong>
                nicht belegt ·

                <strong>${uncertainCount}</strong>
                manuell zu prüfen
            </p>


            <h4>
                ✓ Im Profil belegt
            </h4>

            <div class="requirements-list">
                ${renderRequirements(
                    requirements,
                    0
                )}
            </div>


            <h4>
                △ Nicht im Profil belegt
            </h4>

            <div class="requirements-list">
                ${renderRequirements(
                    requirements,
                    1
                )}
            </div>


            <h4>
                ? Unklar / manuell prüfen
            </h4>

            <div class="requirements-list">
                ${renderRequirements(
                    requirements,
                    2
                )}
            </div>


            <p class="hint">
                „Nicht belegt“ bedeutet, dass die
                Anforderung aktuell nicht durch das
                strukturierte Kandidatenprofil
                nachgewiesen wird.

                Das bedeutet nicht automatisch,
                dass du diese Fähigkeit nicht besitzt.
            </p>


            <h3>
                Bisherige Match-Bewertung
            </h3>

            <p>
                ${esc(
                    m.explanation ||
                    'Keine Erklärung verfügbar.'
                )}
            </p>

           <p class="hint">
    Der Match-Score basiert auf den automatisch erkannten
    Anforderungen der Stellenanzeige und den belegten Angaben
    im Kandidatenprofil. Unklare Anforderungen werden nur
    teilweise gewertet und sollten manuell geprüft werden.
</p>


            <h3>
                Stellendaten
            </h3>


            <table class="analysis-table">

                <tr>
                    <td>
                        Unternehmen
                    </td>

                    <td>
                        ${esc(j.company)}
                    </td>
                </tr>


                <tr>
                    <td>
                        Standort
                    </td>

                    <td>
                        ${esc(
                            j.location ||
                            'Nicht angegeben'
                        )}
                    </td>
                </tr>


                <tr>
                    <td>
                        Gehalt
                    </td>

                    <td>
                        ${salary}
                    </td>
                </tr>


                <tr>
                    <td>
                        Quelle
                    </td>

                    <td>
                        ${esc(j.source)}
                    </td>
                </tr>


                <tr>
                    <td>
                        Status
                    </td>

                    <td>
                        ${esc(x.status)}
                    </td>
                </tr>

            </table>


            <h3>
                Stellenbeschreibung
            </h3>


            <div class="job-description">
                ${j.description || 'Keine Beschreibung verfügbar.'}
            </div>


            <h3>
                Bewerbung
            </h3>


            <div class="actions">

                <a
                    href="/api/job/cv?id=${encodedId}&format=pdf">

                    <button>
                        CV als PDF
                    </button>

                </a>


                <a
                    href="/api/job/cv?id=${encodedId}">

                    <button class="secondary">
                        CV als DOCX
                    </button>

                </a>


                <a
                    href="/api/job/cover-letter?id=${encodedId}&format=pdf">

                    <button class="secondary">
                        Anschreiben als PDF
                    </button>

                </a>


                <a
                    href="/api/job/cover-letter?id=${encodedId}">

                    <button class="secondary">
                        Anschreiben als DOCX
                    </button>

                </a>


                <a
                    href="${esc(j.sourceUrl)}"
                    target="_blank"
                    rel="noopener noreferrer">

                    <button class="secondary">
                        Originalstelle
                    </button>

                </a>

            </div>


            <h3>
                KI-Analyse
            </h3>


            <p>
                Die KI darf ausschließlich vorhandene
                Profilinformationen verwenden und
                keine Erfahrung oder Skills erfinden.
            </p>


            <button
                onclick='aiAnalysis(
                    ${JSON.stringify(id)}
                )'>

                KI-Analyse starten

            </button>


            <pre id="ai"></pre>
        `;

        detail.showModal();
    }
    catch (error) {
        console.error(error);

        alert(
            'Analyse konnte nicht geladen werden: ' +
            error.message
        );
    }
}


// ================================================================
// KI ANALYSE
// ================================================================

async function aiAnalysis(id) {
    $('#ai').textContent =
        'Analysiere …';

    try {
        const result = await api(
            '/api/job/ai-analysis?id=' +
            encodeURIComponent(id),
            {
                method: 'POST'
            }
        );

        $('#ai').textContent =
            result.analysis;
    }
    catch (error) {
        $('#ai').textContent =
            error.message;
    }
}


// ================================================================
// STATUS
// ================================================================

async function setStatus(id, statusValue) {
    await api(
        '/api/job/status?id=' +
        encodeURIComponent(id),
        {
            method: 'PUT',

            headers: {
                'Content-Type':
                    'application/json'
            },

            body: JSON.stringify({
                status: statusValue
            })
        }
    );

    await stats();
}


// ================================================================
// SCAN
// ================================================================

$('#scan').onclick = async () => {
    try {
        $('#msg').textContent =
            'Jobquellen werden aktualisiert …';

        const result = await api(
            '/api/jobs/scan',
            {
                method: 'POST'
            }
        );

        $('#msg').textContent =
            `${result.count} Einträge verarbeitet.`;

        await load();
    }
    catch (error) {
        $('#msg').textContent =
            'Fehler: ' + error.message;
    }
};