import { Component } from "react";
import { ErrorState } from "../shared/ui/ErrorState.jsx";

export class RouteErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidUpdate(previousProps) {
    if (previousProps.resetKey !== this.props.resetKey && this.state.error) {
      this.setState({ error: null });
    }
  }

  render() {
    if (this.state.error) {
      return (
        <ErrorState message="Не удалось загрузить раздел. Возможно, приложение было обновлено.">
          <button
            type="button"
            className="button primary"
            onClick={() => window.location.reload()}
          >
            Обновить страницу
          </button>
        </ErrorState>
      );
    }

    return this.props.children;
  }
}
