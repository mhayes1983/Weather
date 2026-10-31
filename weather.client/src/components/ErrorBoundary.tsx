import { Component, type ReactNode } from 'react';

interface Props {
    message: string;
    children: ReactNode;
}

interface State {
    failed: boolean;
}

// A lazy page can fail to load, for example in a tab left open across a new release.
class ErrorBoundary extends Component<Props, State> {
    state: State = { failed: false };

    static getDerivedStateFromError(): State {
        return { failed: true };
    }

    render() {
        if (!this.state.failed) {
            return this.props.children;
        }

        return (
            <main>
                <p className="error" role="alert">{this.props.message}</p>
                <button type="button" onClick={() => window.location.reload()}>Reload</button>
            </main>
        );
    }
}

export default ErrorBoundary;
