import { LanguageProvider } from "./i18n";
import { AuthProvider } from "./auth/AuthContext";
import { Shell } from "./Shell";

function App() {
  return (
    <LanguageProvider>
      <AuthProvider>
        <Shell />
      </AuthProvider>
    </LanguageProvider>
  );
}

export default App;
