import { createRouter, createWebHistory } from 'vue-router'
import LayoutPage from '../views/LayoutPage.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'layout', component: LayoutPage },
  ],
})

export default router
