import { useState } from "react";
import { SiteHeader } from "./components/SiteHeader";
import { PrimaryNav } from "./components/PrimaryNav";
import { HeroSlider } from "./components/HeroSlider";
import { LoginPage } from "./features/auth/LoginPage";
import { useAuth } from "./auth/AuthContext";
import { NavProvider } from "./app/nav";
import { AppLayout } from "./app/AppLayout";
import "./app/app.css";

type PublicView = "home" | "login";

/** Routes between the PUBLIC site (header + hero + login) and the AUTHENTICATED app. */
export function Shell() {
  const { isAuthenticated } = useAuth();
  const [view, setView] = useState<PublicView>("home");

  if (isAuthenticated) {
    return (
      <NavProvider initial="requests">
        <AppLayout />
      </NavProvider>
    );
  }

  return (
    <>
      <SiteHeader onLoginClick={() => setView("login")} onHomeClick={() => setView("home")} />
      <PrimaryNav />
      {view === "login" ? <LoginPage onSuccess={() => { /* auth flip renders the app */ }} /> : <HeroSlider />}
    </>
  );
}
