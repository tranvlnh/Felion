import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { ApiError } from './api';
import { ToastProvider } from './ui';
import { Layout } from './Layout';
import './index.css';
import { AuditPage } from './pages/AuditPage';
import { DepartmentsPage } from './pages/DepartmentsPage';
import { EmailPage } from './pages/EmailPage';
import { LoginPage } from './pages/LoginPage';
import { MePage } from './pages/MePage';
import { PeoplePage } from './pages/PeoplePage';
import { PersonCreatePage } from './pages/PersonCreatePage';
import { PersonPage } from './pages/PersonPage';
import { ProbationPage } from './pages/ProbationPage';
import { RolesPage } from './pages/RolesPage';
import { SettingsPage } from './pages/SettingsPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
      refetchOnWindowFocus: false,
    },
  },
});

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <MePage /> },
      { path: 'people', element: <PeoplePage /> },
      { path: 'people/new', element: <PersonCreatePage /> },
      { path: 'people/:id', element: <PersonPage /> },
      { path: 'probation', element: <ProbationPage /> },
      { path: 'roles', element: <RolesPage /> },
      { path: 'departments', element: <DepartmentsPage /> },
      { path: 'email', element: <EmailPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'audit', element: <AuditPage /> },
    ],
  },
]);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <RouterProvider router={router} />
      </ToastProvider>
    </QueryClientProvider>
  </StrictMode>,
);
