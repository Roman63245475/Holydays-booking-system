/**
 * This file is the entry point for the React app, it sets up the root
 * element and renders the App component to the DOM.
 *
 * It is included in `src/index.html`.
 */

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import {createBrowserRouter, RouterProvider} from "react-router";
import LogInForm from "@/LogInForm.tsx";
import {Admin} from "@/Admin.tsx";

const elem = document.getElementById("root")!;
const app = (
    <RouterProvider router={createBrowserRouter([
        {
            path: "/",
            element: <LogInForm/>
        },
        {
          path: 'admin_page',
          element: <Admin/>
        }
    ])}/>
);

// https://bun.com/docs/bundler/hot-reloading#import-meta-hot-data
(import.meta.hot.data.root ??= createRoot(elem)).render(app);
