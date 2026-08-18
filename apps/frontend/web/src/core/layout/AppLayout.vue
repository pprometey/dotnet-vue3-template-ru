<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { UI_LOCALES, type UiLocale } from "@core/i18n";
import { isAuthConfigured, signIn, signOut } from "@core/auth/auth-client";
import { isAuthenticated, userEmail } from "@core/auth/session-state";

const { locale, t } = useI18n();

const currentLocale = computed({
  get: () => locale.value as UiLocale,
  set: (value: UiLocale) => {
    locale.value = value;
  },
});
</script>

<template>
  <div class="app-layout">
    <header class="app-layout__header">
      <span class="app-layout__title">{{ t("app.title") }}</span>

      <div class="app-layout__actions">
        <el-select
          v-model="currentLocale"
          :aria-label="t('app.language')"
          size="small"
          class="app-layout__locale"
        >
          <el-option
            v-for="code in UI_LOCALES"
            :key="code"
            :label="code"
            :value="code"
          />
        </el-select>

        <template v-if="isAuthConfigured">
          <template v-if="isAuthenticated">
            <span class="app-layout__user">{{ userEmail }}</span>
            <el-button size="small" @click="signOut()">
              {{ t("app.signOut") }}
            </el-button>
          </template>
          <el-button v-else size="small" type="primary" @click="signIn()">
            {{ t("app.signIn") }}
          </el-button>
        </template>
      </div>
    </header>

    <main class="app-layout__content">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  min-height: 100%;
}

.app-layout__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 24px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
}

.app-layout__title {
  font-weight: 600;
}

.app-layout__actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.app-layout__locale {
  width: 88px;
}

.app-layout__user {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.app-layout__content {
  flex: 1;
  width: 100%;
  max-width: var(--fl-page-max-width);
  margin: 0 auto;
  padding: 24px;
  box-sizing: border-box;
}
</style>
