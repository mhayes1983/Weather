import { useEffect, useState } from 'react';
import { fetchDiscussion, type ForecastDiscussion as Discussion } from '../api/dailyForecast';

interface ForecastDiscussionProps {
    station: string;
}

function issued(time: string): string {
    return new Date(time).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

/** The local NWS office's reasoning. Hides itself when it can't be loaded, because it only adds detail. */
function ForecastDiscussion({ station }: ForecastDiscussionProps) {
    const [discussion, setDiscussion] = useState<Discussion | null>(null);
    const [shownStation, setShownStation] = useState(station);

    // Clearing it stops another station's discussion appearing under this one.
    if (station !== shownStation) {
        setShownStation(station);
        setDiscussion(null);
    }

    useEffect(() => {
        const controller = new AbortController();

        fetchDiscussion(station, controller.signal)
            .then(report => setDiscussion(report.discussion))
            .catch(() => {
                if (!controller.signal.aborted) {
                    setDiscussion(null);
                }
            });

        return () => controller.abort();
    }, [station]);

    if (!discussion) {
        return null;
    }

    const office = encodeURIComponent(discussion.office);

    return (
        <details className="station-card collapsible-card discussion-card">
            <summary><h2>Forecasters' discussion</h2></summary>
            <p className="discussion-issued">
                Written by forecasters at the National Weather Service {discussion.office} office. Issued {issued(discussion.issuedAt)}.
            </p>
            {discussion.sections.map((section, index) =>
                <section key={index}>
                    <h3>{section.title}</h3>
                    {section.paragraphs.map((paragraph, position) => <p key={position}>{paragraph}</p>)}
                </section>
            )}
            <p className="almanac-source">
                From the <a href={`https://forecast.weather.gov/product.php?site=${office}&issuedby=${office}&product=AFD`}>National Weather Service</a>. Watches and warnings are left out here; see the alerts on the home page.
            </p>
        </details>
    );
}

export default ForecastDiscussion;
