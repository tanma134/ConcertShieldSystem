import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import './index.css'
import App from './App.jsx'
import { startRiskTracking } from './utils/riskSignals'

// Risk Engine: bắt đầu theo dõi thao tác thật của người dùng (chuột/chạm/cuộn)
startRiskTracking()

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || '188995023102-6k2nn3r75t2qnuu8m6dp4vkj0jc4p3kl.apps.googleusercontent.com';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <GoogleOAuthProvider clientId={googleClientId}>
      <App />
    </GoogleOAuthProvider>
  </StrictMode>,
)