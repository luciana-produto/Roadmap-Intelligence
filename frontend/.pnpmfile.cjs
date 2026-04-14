function readPackage(pkg) {
  function fixWorkspaceDeps(deps) {
    if (!deps) return deps
    const fixed = { ...deps }
    for (const [name, version] of Object.entries(fixed)) {
      if (typeof version === 'string' && version.startsWith('workspace:')) {
        // workspace:* => *, workspace:^1.0.0 => ^1.0.0, etc.
        const resolved = version.slice('workspace:'.length)
        fixed[name] = resolved === '' || resolved === '*' ? '*' : resolved
      }
    }
    return fixed
  }

  pkg.dependencies = fixWorkspaceDeps(pkg.dependencies)
  pkg.devDependencies = fixWorkspaceDeps(pkg.devDependencies)
  pkg.peerDependencies = fixWorkspaceDeps(pkg.peerDependencies)
  pkg.optionalDependencies = fixWorkspaceDeps(pkg.optionalDependencies)

  return pkg
}

module.exports = {
  hooks: {
    readPackage,
  },
}
