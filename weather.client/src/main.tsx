import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { startAppBridge } from './appBridge'
import { applyTheme, loadTheme } from './theme'

applyTheme(loadTheme())

function render() {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

if (import.meta.env.MODE === 'app') {
  void startAppBridge().then(render)
} else {
  render()
}
