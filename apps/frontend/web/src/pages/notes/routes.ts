import type { RouteRecordRaw } from "vue-router";

// Компонент страницы грузится лениво: это и есть механизм разбиения бандла по
// маршрутам (ADR 0027). Применяется с первого раздела, чтобы не вводить его
// задним числом, когда первая загрузка уже станет тяжёлой.
export const notesRoutes: RouteRecordRaw[] = [
  {
    path: "/notes",
    name: "notes",
    component: () => import("./NotesPage.vue"),
  },
];
