import {
  BarChart3,
  BookOpen,
  CheckCircle2,
  ClipboardCheck,
  FileText,
  Server,
  UsersRound
} from "lucide-react";

export const navigation = {
  admin: [
    { path: "/admin/teachers", label: "Преподаватели", icon: UsersRound },
    { path: "/admin/model-access", label: "Доступ к моделям", icon: Server }
  ],
  teacher: [
    { path: "/teacher/dashboard", label: "Панель", icon: BarChart3 },
    { path: "/teacher/tests", label: "Тесты", icon: BookOpen },
    { path: "/teacher/reviews", label: "Проверки", icon: ClipboardCheck },
    { path: "/teacher/models", label: "Модели", icon: Server }
  ],
  student: [
    { path: "/student/tests", label: "Задания", icon: FileText },
    { path: "/student/results", label: "Результаты", icon: CheckCircle2 }
  ]
};
