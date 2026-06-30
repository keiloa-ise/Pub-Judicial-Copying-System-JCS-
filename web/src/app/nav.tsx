import { createContext, useContext, useState, useCallback, type ReactNode } from "react";

/** Tiny in-app router (no dependency). A page is a name + optional entity id. */
export interface Route { page: string; id?: string; }

interface NavValue {
  route: Route;
  navigate: (page: string, id?: string) => void;
}

const NavContext = createContext<NavValue | null>(null);

export function NavProvider({ initial, children }: { initial: string; children: ReactNode }) {
  const [route, setRoute] = useState<Route>({ page: initial });
  const navigate = useCallback((page: string, id?: string) => setRoute({ page, id }), []);
  return <NavContext.Provider value={{ route, navigate }}>{children}</NavContext.Provider>;
}

export function useNav(): NavValue {
  const ctx = useContext(NavContext);
  if (!ctx) throw new Error("useNav must be used within NavProvider");
  return ctx;
}
